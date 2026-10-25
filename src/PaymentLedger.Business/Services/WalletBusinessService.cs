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
using PaymentLedger.Shared.Context;

namespace PaymentLedger.Business.Services;

public class WalletBusinessService : IWalletBusiness, IWalletBusinessService
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;

    public WalletBusinessService(
        IWalletRepository walletRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        IIdempotencyService idempotencyService,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _idempotencyService = idempotencyService;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
    }

    public async Task<WalletResponse> CreateWalletAsync(CreateWalletRequest request, CancellationToken ct = default)
    {
        ValidateCreateWalletRequest(request);

        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
        {
            throw new DomainException("User not found.", ErrorCodes.UserNotFound);
        }

        if (user.Status != UserStatus.Active)
        {
            throw new DomainException("User is not active.", ErrorCodes.UserNotActive);
        }

        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        var existingWallet = await _walletRepository.GetByUserAndCurrencyAsync(request.UserId, normalizedCurrency, ct);
        if (existingWallet != null)
        {
            throw new DomainException($"User already has a wallet with currency '{normalizedCurrency}'.", ErrorCodes.WalletAlreadyExists);
        }

        var wallet = Wallet.Create(request.UserId, normalizedCurrency, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet, ct);

        await _eventPublisher.PublishAsync(new WalletCreatedEvent(
            WalletId: wallet.Id,
            UserId: wallet.UserId,
            Currency: wallet.Currency,
            OccurredOnUtc: wallet.CreatedAt), ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(wallet);
    }

    public async Task<WalletResponse?> GetWalletByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(id, ct);
        if (wallet != null)
        {
            EnforceWalletOwnership(wallet);
        }
        return wallet == null ? null : MapToResponse(wallet);
    }

    public async Task<WalletBalanceResponse?> GetWalletBalanceAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(id, ct);
        if (wallet == null)
        {
            return null;
        }

        EnforceWalletOwnership(wallet);

        return new WalletBalanceResponse(
            wallet.Id,
            wallet.UserId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status.ToString(),
            wallet.UpdatedAt);
    }

    public async Task<IReadOnlyCollection<WalletResponse>> GetWalletsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        var wallets = await _walletRepository.ListByUserIdAsync(userId, ct);
        return wallets.Select(MapToResponse).ToList();
    }

    public async Task<WalletResponse?> BlockWalletAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(id, ct);
        if (wallet == null)
        {
            return null;
        }

        EnforceWalletOwnership(wallet);

        wallet.Block(_dateTimeProvider.UtcNow);
        _walletRepository.Update(wallet);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(wallet);
    }

    public async Task<WalletResponse?> UnblockWalletAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(id, ct);
        if (wallet == null)
        {
            return null;
        }

        EnforceWalletOwnership(wallet);

        wallet.Unblock(_dateTimeProvider.UtcNow);
        _walletRepository.Update(wallet);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(wallet);
    }

    public async Task<WalletResponse?> CloseWalletAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("WalletId", "Wallet ID is required.");
        }

        var wallet = await _walletRepository.GetByIdAsync(id, ct);
        if (wallet == null)
        {
            return null;
        }

        EnforceWalletOwnership(wallet);

        wallet.Close(_dateTimeProvider.UtcNow);
        _walletRepository.Update(wallet);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(wallet);
    }

    public async Task<TransactionResponse> CreditWalletAsync(
        Guid walletId,
        CreditWalletRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default)
    {
        ValidateCreditWalletRequest(walletId, request);

        var idempotencyKey = _idempotencyService.ResolveKey(
            !string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? idempotencyKeyHeader : request.IdempotencyKey,
            "credit",
            walletId,
            request.ReferenceId,
            request.Amount);

        var endpoint = $"/api/v1/wallets/{walletId}/credit";
        var requestHash = _idempotencyService.ComputeHash(new
        {
            WalletId = walletId,
            Amount = decimal.Round(request.Amount, 2),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            Metadata = request.Metadata?.Trim()
        });

        // 1. Check existing idempotency key
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
            var wallet = await _walletRepository.GetByIdForUpdateAsync(walletId, ct);
            if (wallet == null)
            {
                throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);
            }

            EnforceWalletOwnership(wallet);

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

            if (wallet.Status != WalletStatus.Active)
            {
                throw new DomainException($"Cannot credit wallet with status '{wallet.Status}'. Only active wallets can receive funds.", ErrorCodes.WalletNotActive);
            }

            if (!string.Equals(wallet.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException($"Currency mismatch: wallet currency is '{wallet.Currency}', but credit currency is '{request.Currency}'.", ErrorCodes.CurrencyMismatch);
            }

            var treasuryWallet = await _walletRepository.GetTreasuryWalletAsync(wallet.Currency, ct);
            if (treasuryWallet == null)
            {
                throw new DomainException($"Treasury wallet for currency '{wallet.Currency}' not found.", ErrorCodes.WalletNotFound);
            }

            if (treasuryWallet.Status != WalletStatus.Active)
            {
                throw new DomainException("Treasury wallet is inactive.", ErrorCodes.WalletNotActive);
            }

            var now = _dateTimeProvider.UtcNow;

            // 3. Update target wallet balance
            wallet.Credit(request.Amount, now);
            _walletRepository.Update(wallet);

            // 4. Create Transaction record (Source: Treasury, Destination: User)
            var transaction = Transaction.CreateCredit(
                sourceWalletId: treasuryWallet.Id,
                destinationWalletId: wallet.Id,
                amount: request.Amount,
                currency: wallet.Currency,
                idempotencyKey: idempotencyKey,
                timestamp: now,
                description: request.Description,
                metadata: request.Metadata);

            await _transactionRepository.AddAsync(transaction, ct);

            // 5. Create Double-Entry Ledger records (Treasury Debit, User Credit)
            var treasuryDebitEntry = LedgerEntry.Create(
                transactionId: transaction.Id,
                walletId: treasuryWallet.Id,
                entryType: LedgerEntryType.Debit,
                amount: request.Amount,
                currency: wallet.Currency,
                balanceAfterTransaction: null,
                timestamp: now);

            var userCreditEntry = LedgerEntry.Create(
                transactionId: transaction.Id,
                walletId: wallet.Id,
                entryType: LedgerEntryType.Credit,
                amount: request.Amount,
                currency: wallet.Currency,
                balanceAfterTransaction: wallet.Balance,
                timestamp: now);

            await _ledgerRepository.AddRangeAsync([treasuryDebitEntry, userCreditEntry], ct);

            // 6. Build response
            var result = new TransactionResponse(
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
            await _idempotencyService.SaveAsync(
                idempotencyKey,
                endpoint,
                requestHash,
                result,
                statusCode: 200,
                ct);

            // 8. Publish Outbox Domain Event
            await _eventPublisher.PublishAsync(new TransactionPostedEvent(
                TransactionId: transaction.Id,
                TransactionCode: transaction.TransactionCode,
                Type: transaction.Type.ToString(),
                SourceWalletId: transaction.SourceWalletId,
                DestinationWalletId: transaction.DestinationWalletId,
                Amount: transaction.Amount,
                Currency: transaction.Currency,
                Description: transaction.Description,
                OccurredOnUtc: now,
                ReferenceId: request.ReferenceId), ct);

            // 9. Commit atomic transaction
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

    public async Task<TransactionResponse> DebitWalletAsync(
        Guid walletId,
        DebitWalletRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default)
    {
        ValidateDebitWalletRequest(walletId, request);

        var idempotencyKey = _idempotencyService.ResolveKey(
            !string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? idempotencyKeyHeader : request.IdempotencyKey,
            "debit",
            walletId,
            request.ReferenceId,
            request.Amount);

        var endpoint = $"/api/v1/wallets/{walletId}/debit";
        var requestHash = _idempotencyService.ComputeHash(new
        {
            WalletId = walletId,
            Amount = decimal.Round(request.Amount, 2),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            Metadata = request.Metadata?.Trim()
        });

        // 1. Check existing idempotency key
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
            var wallet = await _walletRepository.GetByIdForUpdateAsync(walletId, ct);
            if (wallet == null)
            {
                throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);
            }

            EnforceWalletOwnership(wallet);

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

            if (wallet.Status != WalletStatus.Active)
            {
                throw new DomainException($"Cannot debit wallet with status '{wallet.Status}'. Only active wallets can perform debit.", ErrorCodes.WalletNotActive);
            }

            if (!string.Equals(wallet.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException($"Currency mismatch: wallet currency is '{wallet.Currency}', but debit currency is '{request.Currency}'.", ErrorCodes.CurrencyMismatch);
            }

            if (wallet.Balance < request.Amount)
            {
                throw new DomainException("Insufficient balance to debit.", ErrorCodes.InsufficientBalance);
            }

            var treasuryWallet = await _walletRepository.GetTreasuryWalletAsync(wallet.Currency, ct);
            if (treasuryWallet == null)
            {
                throw new DomainException($"Treasury wallet for currency '{wallet.Currency}' not found.", ErrorCodes.WalletNotFound);
            }

            if (treasuryWallet.Status != WalletStatus.Active)
            {
                throw new DomainException("Treasury wallet is inactive.", ErrorCodes.WalletNotActive);
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
                idempotencyKey: idempotencyKey,
                timestamp: now,
                description: request.Description,
                metadata: request.Metadata);

            await _transactionRepository.AddAsync(transaction, ct);

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

            await _ledgerRepository.AddRangeAsync([userDebitEntry, treasuryCreditEntry], ct);

            // 6. Build response
            var result = new TransactionResponse(
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
            await _idempotencyService.SaveAsync(
                idempotencyKey,
                endpoint,
                requestHash,
                result,
                statusCode: 200,
                ct);

            // 8. Publish Outbox Domain Event
            await _eventPublisher.PublishAsync(new TransactionPostedEvent(
                TransactionId: transaction.Id,
                TransactionCode: transaction.TransactionCode,
                Type: transaction.Type.ToString(),
                SourceWalletId: transaction.SourceWalletId,
                DestinationWalletId: transaction.DestinationWalletId,
                Amount: transaction.Amount,
                Currency: transaction.Currency,
                Description: transaction.Description,
                OccurredOnUtc: now,
                ReferenceId: request.ReferenceId), ct);

            // 9. Commit atomic transaction
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

        EnforceWalletOwnership(wallet);

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

        EnforceWalletOwnership(wallet);

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

    private static void ValidateCreateWalletRequest(CreateWalletRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.UserId == Guid.Empty)
        {
            errors["UserId"] = ["User ID is required."];
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

    private static void ValidateCreditWalletRequest(Guid walletId, CreditWalletRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (walletId == Guid.Empty)
        {
            errors["WalletId"] = ["Wallet ID is required."];
        }

        if (request.Amount <= 0)
        {
            errors["Amount"] = ["Amount must be greater than zero."];
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

    private static void ValidateDebitWalletRequest(Guid walletId, DebitWalletRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (walletId == Guid.Empty)
        {
            errors["WalletId"] = ["Wallet ID is required."];
        }

        if (request.Amount <= 0)
        {
            errors["Amount"] = ["Amount must be greater than zero."];
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

    private WalletResponse MapToResponse(Wallet wallet)
    {
        return new WalletResponse(
            wallet.Id,
            wallet.UserId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status.ToString(),
            wallet.CreatedAt,
            wallet.UpdatedAt);
    }

    private void EnforceWalletOwnership(Wallet wallet)
    {
        if (_currentUserService.IsAuthenticated && !_currentUserService.IsAdmin && wallet.UserId != _currentUserService.UserId)
        {
            throw new DomainException("You do not have permission to access this wallet.", ErrorCodes.Forbidden);
        }
    }
}



