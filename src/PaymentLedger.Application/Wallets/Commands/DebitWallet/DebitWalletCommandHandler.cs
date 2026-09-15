using MediatR;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Transactions.Common;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentLedger.Application.Wallets.Commands.DebitWallet;

public class DebitWalletCommandHandler : IRequestHandler<DebitWalletCommand, TransactionDto>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DebitWalletCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        IIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<TransactionDto> Handle(DebitWalletCommand request, CancellationToken cancellationToken)
    {
        var endpoint = $"/api/v1/wallets/{request.WalletId}/debit";
        var requestHash = ComputeRequestHash(request);

        // 1. Check existing idempotency key
        var existingIdempotency = await _idempotencyRepository.GetByKeyAndEndpointAsync(
            request.IdempotencyKey,
            endpoint,
            cancellationToken);

        if (existingIdempotency != null)
        {
            if (!string.Equals(existingIdempotency.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new DomainException("IDEMPOTENCY_CONFLICT: Idempotency key was previously used with a different request payload.");
            }

            if (!string.IsNullOrWhiteSpace(existingIdempotency.ResponseBody))
            {
                var cachedResponse = JsonSerializer.Deserialize<TransactionDto>(existingIdempotency.ResponseBody);
                if (cachedResponse != null)
                {
                    return cachedResponse;
                }
            }

            throw new DomainException("A request with this idempotency key is currently being processed.");
        }

        // 2. Wrap in explicit database transaction with row locking
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var wallet = await _walletRepository.GetByIdForUpdateAsync(request.WalletId, cancellationToken);
            if (wallet == null)
            {
                throw new DomainException("Wallet not found.");
            }

            if (wallet.Status != WalletStatus.Active)
            {
                throw new DomainException($"Cannot debit wallet with status '{wallet.Status}'. Only active wallets can perform debit.");
            }

            if (!string.Equals(wallet.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException($"Currency mismatch: wallet currency is '{wallet.Currency}', but debit currency is '{request.Currency}'.");
            }

            if (wallet.Balance < request.Amount)
            {
                throw new DomainException("INSUFFICIENT_BALANCE: Insufficient balance to debit.");
            }

            var treasuryWallet = await _walletRepository.GetTreasuryWalletAsync(wallet.Currency, cancellationToken);
            if (treasuryWallet == null)
            {
                throw new DomainException($"Treasury wallet for currency '{wallet.Currency}' not found.");
            }

            if (treasuryWallet.Status != WalletStatus.Active)
            {
                throw new DomainException("Treasury wallet is inactive.");
            }

            var now = _dateTimeProvider.UtcNow;

            // 3. Deduct target wallet balance
            wallet.Debit(request.Amount, now);
            _walletRepository.Update(wallet);

            // 4. Create Transaction record (Source: User, Destination: Treasury)
            var transaction = Transaction.CreateDebit(
                sourceWalletId: wallet.Id,
                destinationWalletId: treasuryWallet.Id,
                amount: request.Amount,
                currency: wallet.Currency,
                idempotencyKey: request.IdempotencyKey,
                timestamp: now,
                description: request.Description,
                metadata: request.Metadata);

            await _transactionRepository.AddAsync(transaction, cancellationToken);

            // 5. Create Double-Entry Ledger records (User Debit, Treasury Credit)
            var userDebitEntry = LedgerEntry.Create(
                transactionId: transaction.Id,
                walletId: wallet.Id,
                entryType: LedgerEntryType.Debit,
                amount: request.Amount,
                currency: wallet.Currency,
                balanceAfterTransaction: wallet.Balance,
                timestamp: now);

            var treasuryCreditEntry = LedgerEntry.Create(
                transactionId: transaction.Id,
                walletId: treasuryWallet.Id,
                entryType: LedgerEntryType.Credit,
                amount: request.Amount,
                currency: wallet.Currency,
                balanceAfterTransaction: null,
                timestamp: now);

            await _ledgerRepository.AddRangeAsync([userDebitEntry, treasuryCreditEntry], cancellationToken);

            // 6. Build response DTO
            var result = new TransactionDto(
                TransactionId: transaction.Id,
                TransactionCode: transaction.TransactionCode,
                Type: transaction.Type.ToString(),
                Status: transaction.Status.ToString(),
                SourceWalletId: transaction.SourceWalletId,
                DestinationWalletId: transaction.DestinationWalletId,
                Amount: transaction.Amount,
                Currency: transaction.Currency,
                IdempotencyKey: transaction.IdempotencyKey,
                Description: transaction.Description,
                Metadata: transaction.Metadata,
                BalanceAfterTransaction: wallet.Balance,
                CreatedAt: transaction.CreatedAt);

            // 7. Save Idempotency record
            var responseJson = JsonSerializer.Serialize(result);
            var idempotencyRecord = IdempotencyRequest.Create(
                idempotencyKey: request.IdempotencyKey,
                endpoint: endpoint,
                requestHash: requestHash,
                responseBody: responseJson,
                statusCode: 200,
                status: "Completed",
                createdAt: now,
                expiresAt: now.AddHours(24));

            await _idempotencyRepository.AddAsync(idempotencyRecord, cancellationToken);

            // 8. Commit atomic transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static string ComputeRequestHash(DebitWalletCommand request)
    {
        var rawPayload = $"walletId:{request.WalletId:D}|amount:{decimal.Round(request.Amount, 2):0.00}|currency:{request.Currency.Trim().ToUpperInvariant()}|description:{request.Description?.Trim()}|metadata:{request.Metadata?.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));
        return Convert.ToHexStringLower(bytes);
    }
}
