using PaymentLedger.Business.Common;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Business;

public class TransactionBusinessServiceTests
{
    private readonly FakeTransactionRepository _transactionRepository = new();
    private readonly FakeLedgerRepository _ledgerRepository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeIdempotencyService _idempotencyService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);

    private TransactionBusinessService CreateService() =>
        new(
            _transactionRepository,
            _ledgerRepository,
            _walletRepository,
            _idempotencyService,
            _unitOfWork,
            _dateTimeProvider);

    [Fact]
    public async Task RefundTransactionAsync_WithFullRefund_TransitionsOriginalToRefundedAndCreditsUser()
    {
        // Arrange
        var service = CreateService();
        var (userWallet, treasuryWallet, originalTxn) = await SetupDebitTransactionAsync(100m);

        var request = new RefundTransactionRequest(
            Amount: 100m,
            Currency: "USD",
            Reason: "Full refund test");

        // Act
        var result = await service.RefundTransactionAsync(originalTxn.Id, request, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Refund", result.Type);
        Assert.Equal("Posted", result.Status);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(100m, userWallet.Balance); // Original had 0 after debit, now credited back 100
        Assert.Equal(TransactionStatus.Refunded, originalTxn.Status);
        Assert.True(_unitOfWork.TransactionCommitted);

        // Double entry check: 2 ledger entries added
        var entries = _ledgerRepository.Entries.Where(e => e.TransactionId == result.TransactionId).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.WalletId == treasuryWallet.Id && e.EntryType == LedgerEntryType.Debit && e.Amount == 100m);
        Assert.Contains(entries, e => e.WalletId == userWallet.Id && e.EntryType == LedgerEntryType.Credit && e.Amount == 100m);
    }

    [Fact]
    public async Task RefundTransactionAsync_WithPartialRefund_TransitionsOriginalToPartiallyRefunded()
    {
        // Arrange
        var service = CreateService();
        var (userWallet, _, originalTxn) = await SetupDebitTransactionAsync(100m);

        var request = new RefundTransactionRequest(
            Amount: 40m,
            Currency: "USD",
            Reason: "Partial refund");

        // Act
        var result = await service.RefundTransactionAsync(originalTxn.Id, request, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(40m, result.Amount);
        Assert.Equal(40m, userWallet.Balance);
        Assert.Equal(TransactionStatus.PartiallyRefunded, originalTxn.Status);

        // Subsequent refund of remaining 60m should mark it Refunded
        var secondRequest = new RefundTransactionRequest(
            Amount: 60m,
            Currency: "USD",
            Reason: "Remaining refund");

        var secondResult = await service.RefundTransactionAsync(originalTxn.Id, secondRequest, null);
        Assert.Equal(60m, secondResult.Amount);
        Assert.Equal(100m, userWallet.Balance);
        Assert.Equal(TransactionStatus.Refunded, originalTxn.Status);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenAmountExceedsRemaining_ThrowsDomainException()
    {
        // Arrange
        var service = CreateService();
        var (_, _, originalTxn) = await SetupDebitTransactionAsync(100m);

        // First refund 70
        await service.RefundTransactionAsync(
            originalTxn.Id,
            new RefundTransactionRequest(70m, "USD"),
            null);

        // Attempt second refund of 40 (70 + 40 = 110 > 100)
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RefundTransactionAsync(
                originalTxn.Id,
                new RefundTransactionRequest(40m, "USD"),
                null));

        Assert.Equal(ErrorCodes.RefundAmountExceeded, ex.ErrorCode);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenTransactionNotFound_ThrowsDomainException()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RefundTransactionAsync(
                Guid.NewGuid(),
                new RefundTransactionRequest(50m, "USD"),
                null));

        Assert.Equal(ErrorCodes.TransactionNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenTransactionIsCredit_ThrowsDomainException()
    {
        var service = CreateService();
        var creditTxn = Transaction.CreateCredit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            "key-credit",
            _dateTimeProvider.UtcNow);
        await _transactionRepository.AddAsync(creditTxn);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RefundTransactionAsync(
                creditTxn.Id,
                new RefundTransactionRequest(50m, "USD"),
                null));

        Assert.Equal(ErrorCodes.TransactionNotRefundable, ex.ErrorCode);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenCurrencyMismatch_ThrowsDomainException()
    {
        var service = CreateService();
        var (_, _, originalTxn) = await SetupDebitTransactionAsync(100m);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RefundTransactionAsync(
                originalTxn.Id,
                new RefundTransactionRequest(50m, "EUR"),
                null));

        Assert.Equal(ErrorCodes.CurrencyMismatch, ex.ErrorCode);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenWalletBlocked_ThrowsDomainException()
    {
        var service = CreateService();
        var (userWallet, _, originalTxn) = await SetupDebitTransactionAsync(100m);
        userWallet.Block(_dateTimeProvider.UtcNow);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RefundTransactionAsync(
                originalTxn.Id,
                new RefundTransactionRequest(50m, "USD"),
                null));

        Assert.Equal(ErrorCodes.WalletNotActive, ex.ErrorCode);
    }

    [Fact]
    public async Task GetTransactionDetailsAsync_ReturnsDetailsWithLedgerEntriesAndRefunds()
    {
        var service = CreateService();
        var (userWallet, _, originalTxn) = await SetupDebitTransactionAsync(100m);

        // Perform a refund
        await service.RefundTransactionAsync(
            originalTxn.Id,
            new RefundTransactionRequest(30m, "USD", "Partial refund"),
            null);

        var details = await service.GetTransactionDetailsAsync(originalTxn.Id);

        Assert.NotNull(details);
        Assert.Equal(originalTxn.Id, details.TransactionId);
        Assert.Equal(30m, details.TotalRefundedAmount);
        Assert.Equal(70m, details.RemainingRefundableAmount);
        Assert.Single(details.Refunds);
        Assert.Equal(30m, details.Refunds.First().Amount);
    }

    [Fact]
    public async Task GetWalletTransactionsPagedAsync_ReturnsPagedList()
    {
        var service = CreateService();
        var userWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(userWallet);

        var txn1 = Transaction.CreateDebit(userWallet.Id, Guid.NewGuid(), 10m, "USD", "k1", _dateTimeProvider.UtcNow);
        var txn2 = Transaction.CreateCredit(Guid.NewGuid(), userWallet.Id, 50m, "USD", "k2", _dateTimeProvider.UtcNow.AddMinutes(1));
        await _transactionRepository.AddAsync(txn1);
        await _transactionRepository.AddAsync(txn2);

        var paged = await service.GetWalletTransactionsPagedAsync(
            userWallet.Id,
            new GetWalletTransactionsRequest(PageNumber: 1, PageSize: 10));

        Assert.Equal(2, paged.TotalCount);
        Assert.NotNull(paged.Data);
        Assert.Equal(2, paged.Data.Count);
        Assert.Equal(1, paged.PageNumber);
    }

    [Fact]
    public async Task VerifyWalletConsistencyAsync_WhenConsistent_ReturnsTrue()
    {
        var service = CreateService();
        var userWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        userWallet.Credit(100m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(userWallet);

        // Add ledger entry matching the balance
        var entry = LedgerEntry.Create(Guid.NewGuid(), userWallet.Id, LedgerEntryType.Credit, 100m, "USD", 100m, _dateTimeProvider.UtcNow);
        await _ledgerRepository.AddRangeAsync([entry]);

        var audit = await service.VerifyWalletConsistencyAsync(userWallet.Id);

        Assert.True(audit.IsConsistent);
        Assert.Equal(0m, audit.Discrepancy);
        Assert.Equal(100m, audit.CurrentBalance);
        Assert.Equal(100m, audit.CalculatedLedgerBalance);
    }

    [Fact]
    public async Task VerifyWalletConsistencyAsync_WhenDiscrepancyExists_ReturnsFalse()
    {
        var service = CreateService();
        var userWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        userWallet.Credit(100m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(userWallet);

        // Add ledger entry with different amount (e.g. 80m)
        var entry = LedgerEntry.Create(Guid.NewGuid(), userWallet.Id, LedgerEntryType.Credit, 80m, "USD", 80m, _dateTimeProvider.UtcNow);
        await _ledgerRepository.AddRangeAsync([entry]);

        var audit = await service.VerifyWalletConsistencyAsync(userWallet.Id);

        Assert.False(audit.IsConsistent);
        Assert.Equal(20m, audit.Discrepancy);
    }

    [Fact]
    public async Task VerifyTransactionConsistencyAsync_WhenBalanced_ReturnsTrue()
    {
        var service = CreateService();
        var txn = Transaction.CreateDebit(Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", "k-audit", _dateTimeProvider.UtcNow);
        await _transactionRepository.AddAsync(txn);

        var debitEntry = LedgerEntry.Create(txn.Id, txn.SourceWalletId!.Value, LedgerEntryType.Debit, 50m, "USD", null, _dateTimeProvider.UtcNow);
        var creditEntry = LedgerEntry.Create(txn.Id, txn.DestinationWalletId!.Value, LedgerEntryType.Credit, 50m, "USD", null, _dateTimeProvider.UtcNow);
        await _ledgerRepository.AddRangeAsync([debitEntry, creditEntry]);

        var audit = await service.VerifyTransactionConsistencyAsync(txn.Id);

        Assert.True(audit.IsBalanced);
        Assert.Equal(50m, audit.TotalDebits);
        Assert.Equal(50m, audit.TotalCredits);
    }

    private async Task<(Wallet UserWallet, Wallet TreasuryWallet, Transaction DebitTxn)> SetupDebitTransactionAsync(decimal amount)
    {
        var userWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        userWallet.Credit(amount, _dateTimeProvider.UtcNow);
        userWallet.Debit(amount, _dateTimeProvider.UtcNow); // Balance is 0
        await _walletRepository.AddAsync(userWallet);

        var treasuryWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        treasuryWallet.Credit(amount, _dateTimeProvider.UtcNow);
        _walletRepository.SetTreasuryWallet("USD", treasuryWallet);

        var txn = Transaction.CreateDebit(
            userWallet.Id,
            treasuryWallet.Id,
            amount,
            "USD",
            $"key-{Guid.NewGuid():N}",
            _dateTimeProvider.UtcNow);

        await _transactionRepository.AddAsync(txn);

        var userDebit = LedgerEntry.Create(txn.Id, userWallet.Id, LedgerEntryType.Debit, amount, "USD", 0m, _dateTimeProvider.UtcNow);
        var treasuryCredit = LedgerEntry.Create(txn.Id, treasuryWallet.Id, LedgerEntryType.Credit, amount, "USD", null, _dateTimeProvider.UtcNow);
        await _ledgerRepository.AddRangeAsync([userDebit, treasuryCredit]);

        return (userWallet, treasuryWallet, txn);
    }

    #region Fakes

    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();
        private readonly Dictionary<string, Wallet> _treasuries = new(StringComparer.OrdinalIgnoreCase);

        public Task AddAsync(Wallet wallet, CancellationToken ct = default)
        {
            _wallets[wallet.Id] = wallet;
            return Task.CompletedTask;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _wallets.TryGetValue(id, out var w);
            return Task.FromResult(w);
        }

        public Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);

        public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken ct = default) =>
            Task.FromResult(_wallets.Values.FirstOrDefault(w => w.UserId == userId && w.Currency == currency));

        public Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Wallet>>(_wallets.Values.Where(w => w.UserId == userId).ToList());

        public Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken ct = default)
        {
            _treasuries.TryGetValue(currency, out var tw);
            return Task.FromResult(tw);
        }

        public void SetTreasuryWallet(string currency, Wallet wallet)
        {
            _treasuries[currency] = wallet;
            _wallets[wallet.Id] = wallet;
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_wallets.ContainsKey(id));

        public void Update(Wallet wallet) { }
    }

    private sealed class FakeTransactionRepository : ITransactionRepository
    {
        public List<Transaction> Transactions { get; } = new();

        public Task AddAsync(Transaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

        public Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.IdempotencyKey == key));

        public Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(Guid walletId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Transaction>>(Transactions.Where(t => t.SourceWalletId == walletId || t.DestinationWalletId == walletId).ToList());

        public Task<(Transaction Transaction, IReadOnlyCollection<LedgerEntry> Entries, Transaction? ReferenceTransaction)?> GetByIdWithEntriesAsync(
            Guid id,
            CancellationToken ct = default)
        {
            var tx = Transactions.FirstOrDefault(t => t.Id == id);
            if (tx == null) return Task.FromResult<(Transaction Transaction, IReadOnlyCollection<LedgerEntry> Entries, Transaction? ReferenceTransaction)?>(null);
            var refTx = tx.ReferenceTransactionId.HasValue ? Transactions.FirstOrDefault(t => t.Id == tx.ReferenceTransactionId.Value) : null;
            return Task.FromResult<(Transaction Transaction, IReadOnlyCollection<LedgerEntry> Entries, Transaction? ReferenceTransaction)?>((tx, (IReadOnlyCollection<LedgerEntry>)Array.Empty<LedgerEntry>(), refTx));
        }

        public Task<IReadOnlyCollection<Transaction>> GetRefundsForTransactionAsync(Guid originalTransactionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Transaction>>(Transactions.Where(t => t.ReferenceTransactionId == originalTransactionId && t.Status == TransactionStatus.Posted).ToList());

        public Task<decimal> GetTotalRefundedAmountAsync(Guid originalTransactionId, CancellationToken ct = default) =>
            Task.FromResult(Transactions.Where(t => t.ReferenceTransactionId == originalTransactionId && t.Type == TransactionType.Refund && t.Status != TransactionStatus.Failed).Sum(t => t.Amount));

        public Task<(IReadOnlyCollection<Transaction> Items, long TotalCount)> GetPagedByWalletIdAsync(
            Guid walletId,
            int pageNumber,
            int pageSize,
            TransactionType? type = null,
            TransactionStatus? status = null,
            DateTimeOffset? fromDate = null,
            DateTimeOffset? toDate = null,
            CancellationToken ct = default)
        {
            var query = Transactions.Where(t => t.SourceWalletId == walletId || t.DestinationWalletId == walletId);
            if (type.HasValue) query = query.Where(t => t.Type == type.Value);
            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (fromDate.HasValue) query = query.Where(t => t.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(t => t.CreatedAt <= toDate.Value);
            var list = query.OrderByDescending(t => t.CreatedAt).ToList();
            var paged = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(((IReadOnlyCollection<Transaction>)paged, (long)list.Count));
        }
    }

    private sealed class FakeLedgerRepository : ILedgerRepository
    {
        public List<LedgerEntry> Entries { get; } = new();

        public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct = default)
        {
            Entries.AddRange(entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<LedgerEntry>>(Entries.Where(e => e.TransactionId == transactionId).ToList());

        public Task<decimal> GetWalletLedgerSumAsync(Guid walletId, CancellationToken ct = default)
        {
            var credits = Entries.Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Credit).Sum(e => e.Amount);
            var debits = Entries.Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Debit).Sum(e => e.Amount);
            return Task.FromResult(credits - debits);
        }

        public Task<(decimal TotalDebits, decimal TotalCredits)> GetTransactionLedgerSumAsync(Guid transactionId, CancellationToken ct = default)
        {
            var debits = Entries.Where(e => e.TransactionId == transactionId && e.EntryType == LedgerEntryType.Debit).Sum(e => e.Amount);
            var credits = Entries.Where(e => e.TransactionId == transactionId && e.EntryType == LedgerEntryType.Credit).Sum(e => e.Amount);
            return Task.FromResult((debits, credits));
        }

        public Task<SystemLedgerAuditResult> GetSystemLedgerDiscrepanciesAsync(CancellationToken ct = default) =>
            Task.FromResult(new SystemLedgerAuditResult(
                Array.Empty<TransactionDiscrepancy>(),
                Array.Empty<WalletDiscrepancy>(),
                0,
                0,
                Entries.Count));
    }

    private sealed class FakeIdempotencyService : IIdempotencyService
    {
        public IdempotencyCheckResult<TransactionResponse>? NextCheckResult { get; set; }

        public string ResolveKey(string? headerKey, string action, Guid walletId, string? referenceId, decimal? amount = null) =>
            !string.IsNullOrWhiteSpace(headerKey) ? headerKey : $"key-{walletId}-{action}";

        public void CheckAntiDoubleClick(Guid walletId, decimal amount, string action, TimeSpan? window = null) { }

        public string ComputeHash<T>(T payload) => "fake-hash";

        public Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(string key, string endpoint, string hash, CancellationToken ct) =>
            Task.FromResult((IdempotencyCheckResult<TResult>?)(object?)NextCheckResult ?? IdempotencyCheckResult<TResult>.None());

        public Task SaveAsync<TResult>(string key, string endpoint, string hash, TResult response, int statusCode, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool ChangesSaved { get; private set; }
        public bool TransactionBegun { get; private set; }
        public bool TransactionCommitted { get; private set; }
        public bool TransactionRolledBack { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ChangesSaved = true;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken ct = default)
        {
            TransactionBegun = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken ct = default)
        {
            TransactionCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            TransactionRolledBack = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; }
        public FakeDateTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;
    }

    #endregion
}
