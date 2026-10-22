using PaymentLedger.Business.Common;
using PaymentLedger.Business.Events;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Context;

namespace PaymentLedger.UnitTests.Business;

public class WalletBusinessServiceTests
{
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeTransactionRepository _transactionRepository = new();
    private readonly FakeLedgerRepository _ledgerRepository = new();
    private readonly FakeIdempotencyService _idempotencyService = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);
    private readonly FakeCurrentUserService _currentUserService = new();

    private WalletBusinessService CreateService() =>
        new(
            _walletRepository,
            _userRepository,
            _transactionRepository,
            _ledgerRepository,
            _idempotencyService,
            _eventPublisher,
            _unitOfWork,
            _dateTimeProvider,
            _currentUserService);

    [Fact]
    public async Task CreateWalletAsync_WithValidData_CreatesWallet()
    {
        var service = CreateService();
        var user = User.Create("Alice", "alice@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var request = new CreateWalletRequest(user.Id, "USD");
        var response = await service.CreateWalletAsync(request);

        Assert.NotEqual(Guid.Empty, response.WalletId);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal("USD", response.Currency);
        Assert.Equal(0m, response.Balance);
        Assert.Equal("Active", response.Status);
        Assert.True(_unitOfWork.ChangesSaved);
        Assert.Single(_eventPublisher.PublishedEvents);
        var publishedEvent = Assert.IsType<WalletCreatedEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(response.WalletId, publishedEvent.WalletId);
        Assert.Equal(user.Id, publishedEvent.UserId);
        Assert.Equal("USD", publishedEvent.Currency);
    }

    [Fact]
    public async Task CreateWalletAsync_WhenUserNotFound_ThrowsDomainException()
    {
        var service = CreateService();
        var request = new CreateWalletRequest(Guid.NewGuid(), "USD");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateWalletAsync(request));
        Assert.Equal(ErrorCodes.UserNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateWalletAsync_WhenDuplicateCurrencyForUser_ThrowsDomainException()
    {
        var service = CreateService();
        var user = User.Create("Alice", "alice@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var existingWallet = Wallet.Create(user.Id, "USD", _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(existingWallet);

        var request = new CreateWalletRequest(user.Id, "USD");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateWalletAsync(request));
        Assert.Equal(ErrorCodes.WalletAlreadyExists, ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("123")]
    public async Task CreateWalletAsync_WithInvalidCurrency_ThrowsValidationException(string currency)
    {
        var service = CreateService();
        var request = new CreateWalletRequest(Guid.NewGuid(), currency);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateWalletAsync(request));
    }

    [Fact]
    public async Task GetWalletBalanceAsync_WhenWalletExists_ReturnsBalance()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(150m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var response = await service.GetWalletBalanceAsync(wallet.Id);

        Assert.NotNull(response);
        Assert.Equal(150m, response.AvailableBalance);
        Assert.Equal("USD", response.Currency);
    }

    [Fact]
    public async Task BlockWalletAsync_BlocksWallet()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var response = await service.BlockWalletAsync(wallet.Id);

        Assert.NotNull(response);
        Assert.Equal("Blocked", response.Status);
    }

    [Fact]
    public async Task CloseWalletAsync_WhenBalanceNonZero_ThrowsDomainException()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(50m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        await Assert.ThrowsAsync<DomainException>(() => service.CloseWalletAsync(wallet.Id));
    }

    [Fact]
    public async Task CreditWalletAsync_WhenIdempotencyCached_ReturnsCached()
    {
        var service = CreateService();
        var walletId = Guid.NewGuid();
        var cached = new TransactionResponse(
            Guid.NewGuid(), "TX-CACHED", "Credit", "Posted",
            Guid.NewGuid(), walletId, 100m, "USD", "idem-key", null, null, 100m, _dateTimeProvider.UtcNow);

        _idempotencyService.NextCheckResult = IdempotencyCheckResult<TransactionResponse>.Processed(cached);

        var response = await service.CreditWalletAsync(walletId, new CreditWalletRequest(100m, "USD", "idem-key"), null);

        Assert.Equal("TX-CACHED", response.TransactionCode);
        Assert.False(_unitOfWork.TransactionBegun);
    }

    [Fact]
    public async Task CreditWalletAsync_WithValidRequest_CreditsAndCreatesDoubleEntry()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var treasury = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        _walletRepository.SetTreasuryWallet("USD", treasury);

        var request = new CreditWalletRequest(100m, "USD", "credit-key-1");
        var response = await service.CreditWalletAsync(wallet.Id, request, null);

        Assert.Equal(100m, response.Amount);
        Assert.Equal(100m, wallet.Balance);
        Assert.Equal(100m, response.BalanceAfterTransaction);
        Assert.True(_unitOfWork.TransactionCommitted);
        Assert.Single(_transactionRepository.Transactions);
        Assert.Equal(2, _ledgerRepository.Entries.Count);
        Assert.Single(_eventPublisher.PublishedEvents);
        var publishedEvent = Assert.IsType<TransactionPostedEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(response.TransactionId, publishedEvent.TransactionId);
        Assert.Equal("Credit", publishedEvent.Type);
        Assert.Equal(100m, publishedEvent.Amount);
        Assert.Equal("USD", publishedEvent.Currency);
    }

    [Fact]
    public async Task DebitWalletAsync_WhenInsufficientBalance_ThrowsDomainException()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(50m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var treasury = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        _walletRepository.SetTreasuryWallet("USD", treasury);

        var request = new DebitWalletRequest(100m, "USD", "debit-key-1");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DebitWalletAsync(wallet.Id, request, null));
        Assert.Equal(ErrorCodes.InsufficientBalance, ex.ErrorCode);
        Assert.True(_unitOfWork.TransactionRolledBack);
    }

    [Fact]
    public async Task DebitWalletAsync_WithSufficientBalance_DebitsAndCreatesDoubleEntry()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(100m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var treasury = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        _walletRepository.SetTreasuryWallet("USD", treasury);

        var request = new DebitWalletRequest(40m, "USD", "debit-key-2");
        var response = await service.DebitWalletAsync(wallet.Id, request, null);

        Assert.Equal(40m, response.Amount);
        Assert.Equal(60m, wallet.Balance);
        Assert.Equal(60m, response.BalanceAfterTransaction);
        Assert.True(_unitOfWork.TransactionCommitted);
        Assert.Single(_transactionRepository.Transactions);
        Assert.Equal(2, _ledgerRepository.Entries.Count);
        Assert.Single(_eventPublisher.PublishedEvents);
        var publishedEvent = Assert.IsType<TransactionPostedEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(response.TransactionId, publishedEvent.TransactionId);
        Assert.Equal("Debit", publishedEvent.Type);
        Assert.Equal(40m, publishedEvent.Amount);
        Assert.Equal("USD", publishedEvent.Currency);
    }

    [Fact]
    public async Task DebitWalletAsync_WhenPostLockCheckFindsProcessedIdempotency_RollsBackAndReturnsCached()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(100m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var cached = new TransactionResponse(
            Guid.NewGuid(), "TX-POSTLOCK", "Debit", "Posted",
            wallet.Id, Guid.NewGuid(), 25m, "USD", "idem-key", null, null, 75m, _dateTimeProvider.UtcNow);

        _idempotencyService.NextCheckResult = IdempotencyCheckResult<TransactionResponse>.None();
        _idempotencyService.PostLockCheckResult = IdempotencyCheckResult<TransactionResponse>.Processed(cached);

        var request = new DebitWalletRequest(25m, "USD", "idem-key");
        var response = await service.DebitWalletAsync(wallet.Id, request, null);

        Assert.Equal("TX-POSTLOCK", response.TransactionCode);
        Assert.True(_unitOfWork.TransactionRolledBack);
        Assert.False(_unitOfWork.TransactionCommitted);
    }

    [Fact]
    public async Task DebitWalletAsync_WhenWalletBlocked_ThrowsDomainException()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(100m, _dateTimeProvider.UtcNow);
        wallet.Block(_dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var request = new DebitWalletRequest(25m, "USD", "idem-blocked");
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DebitWalletAsync(wallet.Id, request, null));

        Assert.Equal(ErrorCodes.WalletNotActive, ex.ErrorCode);
        Assert.True(_unitOfWork.TransactionRolledBack);
    }

    [Fact]
    public async Task DebitWalletAsync_WhenCurrencyMismatch_ThrowsDomainException()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Credit(100m, _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var request = new DebitWalletRequest(25m, "INR", "idem-mismatch");
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DebitWalletAsync(wallet.Id, request, null));

        Assert.Equal(ErrorCodes.CurrencyMismatch, ex.ErrorCode);
        Assert.True(_unitOfWork.TransactionRolledBack);
    }

    [Fact]
    public async Task CreditWalletAsync_WhenWalletBlocked_ThrowsDomainException()
    {
        var service = CreateService();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);
        wallet.Block(_dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var request = new CreditWalletRequest(50m, "USD", "idem-credit-blocked");
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreditWalletAsync(wallet.Id, request, null));

        Assert.Equal(ErrorCodes.WalletNotActive, ex.ErrorCode);
        Assert.True(_unitOfWork.TransactionRolledBack);
    }

    #region Fakes

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = new();

        public Task AddAsync(User user, CancellationToken ct = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<User>>(_users);

        public Task<(IReadOnlyList<User> Users, long TotalCount)> GetPagedAsync(
            int pageNumber, int pageSize, string? searchText = null, UserStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<User>)_users, (long)_users.Count));

        public void Update(User user) { }
        public void Delete(User user) => _users.Remove(user);
    }

    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();
        private readonly Dictionary<string, Wallet> _treasuries = new();

        public void SetTreasuryWallet(string currency, Wallet wallet) => _treasuries[currency] = wallet;

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
        private int _checkCount;
        public IdempotencyCheckResult<TransactionResponse>? NextCheckResult { get; set; }
        public IdempotencyCheckResult<TransactionResponse>? PostLockCheckResult { get; set; }

        public string ResolveKey(string? headerKey, string action, Guid walletId, string? referenceId, decimal? amount = null) =>
            !string.IsNullOrWhiteSpace(headerKey) ? headerKey : $"key-{walletId}";

        public void CheckAntiDoubleClick(Guid walletId, decimal amount, string action, TimeSpan? window = null) { }

        public string ComputeHash<T>(T payload) => "fake-hash";

        public Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(string key, string endpoint, string hash, CancellationToken ct)
        {
            _checkCount++;
            if (_checkCount >= 2 && PostLockCheckResult != null)
            {
                return Task.FromResult((IdempotencyCheckResult<TResult>?)(object?)PostLockCheckResult ?? IdempotencyCheckResult<TResult>.None());
            }

            return Task.FromResult((IdempotencyCheckResult<TResult>?)(object?)NextCheckResult ?? IdempotencyCheckResult<TResult>.None());
        }

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

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => Guid.Empty;
        public string Email => string.Empty;
        public string Role => string.Empty;
        public IReadOnlyCollection<string> Scopes => Array.Empty<string>();
        public bool IsAdmin => false;
        public bool HasScope(string scope) => false;
    }

    private sealed class FakeEventPublisher : IEventPublisher
    {
        public List<IDomainEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
            where TEvent : IDomainEvent
        {
            PublishedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    #endregion
}



