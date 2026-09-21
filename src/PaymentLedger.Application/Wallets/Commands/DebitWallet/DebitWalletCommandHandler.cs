using MediatR;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Transactions.Common;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.Application.Wallets.Commands.DebitWallet;

public class DebitWalletCommandHandler : IRequestHandler<DebitWalletCommand, TransactionDto>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DebitWalletCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ILedgerRepository ledgerRepository,
        IIdempotencyService idempotencyService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _idempotencyService = idempotencyService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<TransactionDto> Handle(DebitWalletCommand request, CancellationToken cancellationToken)
    {
        var endpoint = $"/api/v1/wallets/{request.WalletId}/debit";
        var requestHash = _idempotencyService.ComputeHash(new
        {
            request.WalletId,
            Amount = decimal.Round(request.Amount, 2),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            Metadata = request.Metadata?.Trim()
        });

        // 1. Check existing idempotency key
        var idempotencyCheck = await _idempotencyService.CheckAsync<TransactionDto>(
            request.IdempotencyKey,
            endpoint,
            requestHash,
            cancellationToken);

        if (idempotencyCheck.IsProcessed && idempotencyCheck.Result != null)
        {
            return idempotencyCheck.Result;
        }

        // 2. Wrap in explicit database transaction with row locking
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var wallet = await _walletRepository.GetByIdForUpdateAsync(request.WalletId, cancellationToken);
            if (wallet == null)
            {
                throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);
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

            var treasuryWallet = await _walletRepository.GetTreasuryWalletAsync(wallet.Currency, cancellationToken);
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
            await _idempotencyService.SaveAsync(
                request.IdempotencyKey,
                endpoint,
                requestHash,
                result,
                statusCode: 200,
                cancellationToken);

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
}
