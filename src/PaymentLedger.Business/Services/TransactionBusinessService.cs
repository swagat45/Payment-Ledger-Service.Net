using PaymentLedger.Business.Common;
using PaymentLedger.Business.Events;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Business.Models;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Services;

public class TransactionBusinessService : ITransactionBusinessService, ITransactionBusiness
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TransactionBusinessService(
        ITransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        IWalletRepository walletRepository,
        IIdempotencyService idempotencyService,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _walletRepository = walletRepository;
        _idempotencyService = idempotencyService;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<TransactionResponse> RefundTransactionAsync(
        Guid transactionId,
        RefundTransactionRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default)
    {
        ValidateRefundRequest(transactionId, request);

        var idempotencyKey = _idempotencyService.ResolveKey(
            !string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? idempotencyKeyHeader : request.IdempotencyKey,
            "refund",
            transactionId,
            request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Amount);

        var endpoint = $"/api/v1/transactions/{transactionId}/refund";
        var requestHash = _idempotencyService.ComputeHash(new
        {
            TransactionId = transactionId,
            Amount = decimal.Round(request.Amount, 2),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Reason = request.Reason?.Trim(),
            Metadata = request.Metadata?.Trim()
        });

        // 1. Check idempotency
        var idempotencyCheck = await _idempotencyService.CheckAsync<TransactionResponse>(
            idempotencyKey,
            endpoint,
            requestHash,
            ct);

        if (idempotencyCheck.IsProcessed && idempotencyCheck.Result != null)
        {
            return idempotencyCheck.Result;
        }

        // 2. Wrap in explicit database transaction with row locking
        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var originalTxn = await _transactionRepository.GetByIdAsync(transactionId, ct);
            if (originalTxn == null)
            {
                throw new DomainException("Transaction not found.", ErrorCodes.TransactionNotFound);
            }

            if (originalTxn.Type != TransactionType.Debit)
            {
                throw new DomainException(
                    $"Cannot refund a transaction of type '{originalTxn.Type}'. Only debit transactions are refundable.",
                    ErrorCodes.TransactionNotRefundable);
            }

            if (originalTxn.Status != TransactionStatus.Posted && originalTxn.Status != TransactionStatus.PartiallyRefunded)
            {
                throw new DomainException(
                    $"Cannot refund transaction with status '{originalTxn.Status}'. Only posted or partially refunded transactions can be refunded.",
                    ErrorCodes.TransactionNotRefundable);
            }

            if (!string.Equals(originalTxn.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException(
                    $"Currency mismatch: transaction currency is '{originalTxn.Currency}', but refund currency is '{request.Currency}'.",
                    ErrorCodes.CurrencyMismatch);
            }

            if (!originalTxn.SourceWalletId.HasValue)
            {
                throw new DomainException("Source wallet ID is missing on the original transaction.", ErrorCodes.WalletNotFound);
            }

            var alreadyRefunded = await _transactionRepository.GetTotalRefundedAmountAsync(transactionId, ct);
            var requestedRefundAmount = decimal.Round(request.Amount, 2, MidpointRounding.ToEven);
            var remainingRefundable = originalTxn.Amount - alreadyRefunded;

            if (requestedRefundAmount > remainingRefundable)
            {
                throw new DomainException(
                    $"Refund amount ({requestedRefundAmount:F2}) exceeds remaining refundable amount ({remainingRefundable:F2}). Original amount: {originalTxn.Amount:F2}, already refunded: {alreadyRefunded:F2}.",
                    ErrorCodes.RefundAmountExceeded);
            }

            var userWallet = await _walletRepository.GetByIdForUpdateAsync(originalTxn.SourceWalletId.Value, ct);
            if (userWallet == null)
            {
                throw new DomainException("User wallet not found.", ErrorCodes.WalletNotFound);
            }

            var postLockCheck = await _idempotencyService.CheckAsync<TransactionResponse>(
                idempotencyKey,
                endpoint,
                requestHash,
                ct);

            if (postLockCheck.IsProcessed && postLockCheck.Result != null)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                return postLockCheck.Result;
            }

            if (userWallet.Status != WalletStatus.Active)
            {
                throw new DomainException($"Cannot refund to wallet with status '{userWallet.Status}'. Only active wallets can receive funds.", ErrorCodes.WalletNotActive);
            }

            var treasuryWallet = await _walletRepository.GetTreasuryWalletAsync(originalTxn.Currency, ct);
            if (treasuryWallet == null)
            {
                throw new DomainException($"Treasury wallet for currency '{originalTxn.Currency}' not found.", ErrorCodes.WalletNotFound);
            }

            if (treasuryWallet.Status != WalletStatus.Active)
            {
                throw new DomainException("Treasury wallet is inactive.", ErrorCodes.WalletNotActive);
            }

            var now = _dateTimeProvider.UtcNow;

            // 3. Return funds to User Wallet
            userWallet.Credit(requestedRefundAmount, now);
            _walletRepository.Update(userWallet);

            // 4. Create Refund Transaction (Source: Treasury, Destination: User)
            var refundTxn = Transaction.CreateRefund(
                originalTransactionId: originalTxn.Id,
                sourceWalletId: treasuryWallet.Id,
                destinationWalletId: userWallet.Id,
                amount: requestedRefundAmount,
                currency: originalTxn.Currency,
                idempotencyKey: idempotencyKey,
                timestamp: now,
                description: request.Reason ?? $"Refund for {originalTxn.TransactionCode}",
                metadata: request.Metadata);

            await _transactionRepository.AddAsync(refundTxn, ct);

            // 5. Create Double-Entry Ledger Entries (Treasury Debit, User Credit)
            var treasuryDebitEntry = LedgerEntry.Create(
                transactionId: refundTxn.Id,
                walletId: treasuryWallet.Id,
                entryType: LedgerEntryType.Debit,
                amount: requestedRefundAmount,
                currency: originalTxn.Currency,
                balanceAfterTransaction: null,
                timestamp: now);

            var userCreditEntry = LedgerEntry.Create(
                transactionId: refundTxn.Id,
                walletId: userWallet.Id,
                entryType: LedgerEntryType.Credit,
                amount: requestedRefundAmount,
                currency: originalTxn.Currency,
                balanceAfterTransaction: userWallet.Balance,
                timestamp: now);

            await _ledgerRepository.AddRangeAsync([treasuryDebitEntry, userCreditEntry], ct);

            // 6. Update Original Transaction Status
            if (alreadyRefunded + requestedRefundAmount == originalTxn.Amount)
            {
                originalTxn.MarkRefunded(now);
            }
            else
            {
                originalTxn.MarkPartiallyRefunded(now);
            }

            // 7. Build response
            var result = new TransactionResponse(
                TransactionId: refundTxn.Id,
                TransactionCode: refundTxn.TransactionCode,
                Type: refundTxn.Type.ToString(),
                Status: refundTxn.Status.ToString(),
                SourceWalletId: refundTxn.SourceWalletId,
                DestinationWalletId: refundTxn.DestinationWalletId,
                Amount: refundTxn.Amount,
                Currency: refundTxn.Currency,
                IdempotencyKey: refundTxn.IdempotencyKey,
                Description: refundTxn.Description,
                Metadata: refundTxn.Metadata,
                BalanceAfterTransaction: userWallet.Balance,
                CreatedAt: refundTxn.CreatedAt);

            // 8. Save Idempotency
            await _idempotencyService.SaveAsync(
                idempotencyKey,
                endpoint,
                requestHash,
                result,
                statusCode: 200,
                ct);

            // 9. Publish Outbox Domain Event
            await _eventPublisher.PublishAsync(new TransactionPostedEvent(
                TransactionId: refundTxn.Id,
                TransactionCode: refundTxn.TransactionCode,
                Type: "Refund",
                SourceWalletId: refundTxn.SourceWalletId,
                DestinationWalletId: refundTxn.DestinationWalletId,
                Amount: refundTxn.Amount,
                Currency: refundTxn.Currency,
                Description: refundTxn.Description,
                OccurredOnUtc: now), ct);

            // 10. Commit transaction
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<TransactionDetailsResponse?> GetTransactionDetailsAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ValidationException("TransactionId", "Transaction ID is required.");
        }

        var record = await _transactionRepository.GetByIdWithEntriesAsync(transactionId, ct);
        if (record == null)
        {
            return null;
        }

        var (transaction, entries, _) = record.Value;

        var childRefunds = await _transactionRepository.GetRefundsForTransactionAsync(transactionId, ct);
        var totalRefunded = childRefunds.Sum(r => r.Amount);
        var remainingRefundable = transaction.Type == TransactionType.Debit ? Math.Max(0m, transaction.Amount - totalRefunded) : 0m;

        var refundDtos = childRefunds.Select(r => new RefundSummaryDto(
            r.Id,
            r.TransactionCode,
            r.Amount,
            r.Currency,
            r.Status.ToString(),
            r.CreatedAt)).ToList();

        var entryDtos = entries.Select(e => new LedgerEntryDto(
            e.Id,
            e.TransactionId,
            e.WalletId,
            e.EntryType.ToString(),
            e.Amount,
            e.Currency,
            e.BalanceAfterTransaction,
            e.CreatedAt)).ToList();

        decimal? balanceAfterTx = null;
        var userEntry = entries.FirstOrDefault(e => e.BalanceAfterTransaction.HasValue);
        if (userEntry != null)
        {
            balanceAfterTx = userEntry.BalanceAfterTransaction;
        }

        return new TransactionDetailsResponse(
            TransactionId: transaction.Id,
            TransactionCode: transaction.TransactionCode,
            Type: transaction.Type.ToString(),
            Status: transaction.Status.ToString(),
            SourceWalletId: transaction.SourceWalletId,
            DestinationWalletId: transaction.DestinationWalletId,
            Amount: transaction.Amount,
            Currency: transaction.Currency,
            IdempotencyKey: transaction.IdempotencyKey,
            ReferenceTransactionId: transaction.ReferenceTransactionId,
            Description: transaction.Description,
            Metadata: transaction.Metadata,
            BalanceAfterTransaction: balanceAfterTx,
            CreatedAt: transaction.CreatedAt,
            UpdatedAt: transaction.UpdatedAt,
            LedgerEntries: entryDtos,
            TotalRefundedAmount: totalRefunded,
            RemainingRefundableAmount: remainingRefundable,
            Refunds: refundDtos);
    }

    public async Task<PagedResponse<TransactionResponse>> GetWalletTransactionsPagedAsync(
        Guid walletId,
        GetWalletTransactionsRequest request,
        CancellationToken ct = default)
    {
        if (walletId == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(walletId, ct);
        if (wallet == null)
        {
            throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);
        }

        if (request.PageNumber < 1)
        {
            throw new ValidationException("PageNumber", "Page number must be at least 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ValidationException("PageSize", "Page size must be between 1 and 100.");
        }

        TransactionType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type) &&
            Enum.TryParse<TransactionType>(request.Type, true, out var parsedType))
        {
            type = parsedType;
        }

        TransactionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<TransactionStatus>(request.Status, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var (items, totalCount) = await _transactionRepository.GetPagedByWalletIdAsync(
            walletId,
            request.PageNumber,
            request.PageSize,
            type,
            status,
            request.FromDate,
            request.ToDate,
            ct);

        var responses = items.Select(t => new TransactionResponse(
            t.Id,
            t.TransactionCode,
            t.Type.ToString(),
            t.Status.ToString(),
            t.SourceWalletId,
            t.DestinationWalletId,
            t.Amount,
            t.Currency,
            t.IdempotencyKey,
            t.Description,
            t.Metadata,
            null,
            t.CreatedAt)).ToList();

        return PagedResponse<TransactionResponse>.Ok(
            responses,
            request.PageNumber,
            request.PageSize,
            totalCount,
            "Wallet transactions retrieved successfully.");
    }

    public async Task<WalletAuditResponse> VerifyWalletConsistencyAsync(
        Guid walletId,
        CancellationToken ct = default)
    {
        if (walletId == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(walletId, ct);
        if (wallet == null)
        {
            throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);
        }

        var calculatedLedgerBalance = await _ledgerRepository.GetWalletLedgerSumAsync(walletId, ct);
        var discrepancy = decimal.Round(wallet.Balance - calculatedLedgerBalance, 2);
        var isConsistent = discrepancy == 0m;

        return new WalletAuditResponse(
            WalletId: wallet.Id,
            UserId: wallet.UserId,
            Currency: wallet.Currency,
            CurrentBalance: wallet.Balance,
            CalculatedLedgerBalance: calculatedLedgerBalance,
            Discrepancy: discrepancy,
            IsConsistent: isConsistent,
            AuditedAt: _dateTimeProvider.UtcNow);
    }

    public async Task<TransactionAuditResponse> VerifyTransactionConsistencyAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ValidationException("TransactionId", "Transaction ID is required.");
        }

        var txn = await _transactionRepository.GetByIdAsync(transactionId, ct);
        if (txn == null)
        {
            throw new DomainException("Transaction not found.", ErrorCodes.TransactionNotFound);
        }

        var (totalDebits, totalCredits) = await _ledgerRepository.GetTransactionLedgerSumAsync(transactionId, ct);
        var isBalanced = totalDebits == totalCredits && totalDebits == txn.Amount;

        string message;
        if (totalDebits != totalCredits)
        {
            message = $"Transaction entries unbalanced: total debits ({totalDebits}) != total credits ({totalCredits}).";
        }
        else if (totalDebits != txn.Amount)
        {
            message = $"Transaction entry sum ({totalDebits}) does not match transaction amount ({txn.Amount}).";
        }
        else
        {
            message = "Transaction double-entry ledger is perfectly balanced.";
        }

        return new TransactionAuditResponse(
            TransactionId: txn.Id,
            TransactionCode: txn.TransactionCode,
            TransactionAmount: txn.Amount,
            TotalDebits: totalDebits,
            TotalCredits: totalCredits,
            IsBalanced: isBalanced,
            Message: message,
            AuditedAt: _dateTimeProvider.UtcNow);
    }

    public async Task<LedgerConsistencyResponse> VerifySystemLedgerConsistencyAsync(
        CancellationToken ct = default)
    {
        var auditResult = await _ledgerRepository.GetSystemLedgerDiscrepanciesAsync(ct);

        var txnDiscrepancyDtos = auditResult.TransactionDiscrepancies.Select(td => new TransactionDiscrepancyDto(
            td.TransactionId,
            td.TransactionCode,
            td.TransactionAmount,
            td.TotalDebits,
            td.TotalCredits,
            td.Reason)).ToList();

        var walletDiscrepancyDtos = auditResult.WalletDiscrepancies.Select(wd => new WalletDiscrepancyDto(
            wd.WalletId,
            wd.UserId,
            wd.Currency,
            wd.RecordedBalance,
            wd.CalculatedLedgerBalance,
            wd.Discrepancy)).ToList();

        var isConsistent = txnDiscrepancyDtos.Count == 0 && walletDiscrepancyDtos.Count == 0;

        return new LedgerConsistencyResponse(
            IsConsistent: isConsistent,
            TotalWalletsAudited: auditResult.TotalWalletsChecked,
            TotalTransactionsAudited: auditResult.TotalTransactionsChecked,
            TotalLedgerEntriesAudited: auditResult.TotalLedgerEntriesChecked,
            TransactionDiscrepancies: txnDiscrepancyDtos,
            WalletDiscrepancies: walletDiscrepancyDtos,
            AuditedAt: _dateTimeProvider.UtcNow);
    }

    private static void ValidateRefundRequest(Guid transactionId, RefundTransactionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (transactionId == Guid.Empty)
        {
            errors["TransactionId"] = ["Transaction ID is required."];
        }

        if (request == null)
        {
            errors["Request"] = ["Request body is required."];
            throw new ValidationException(errors);
        }

        if (request.Amount <= 0)
        {
            errors["Amount"] = ["Refund amount must be greater than zero."];
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            errors["Currency"] = ["Currency is required."];
        }
        else
        {
            var trimmed = request.Currency.Trim();
            if (trimmed.Length != 3 || !trimmed.All(char.IsLetter))
            {
                errors["Currency"] = ["Currency must be a valid 3-letter code."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
