using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Transactions.Common;
using PaymentLedger.Application.Wallets.Commands.CreditWallet;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Application.Wallets;

public class CreditWalletCommandHandlerTests
{
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeTransactionRepository _transactionRepository = new();
    private readonly FakeLedgerRepository _ledgerRepository = new();
    private readonly FakeIdempotencyService _idempotencyService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);

    private CreditWalletCommandHandler CreateHandler() =>
        new(
            _walletRepository,
            _transactionRepository,
            _ledgerRepository,
            _idempotencyService,
            _unitOfWork,
            _dateTimeProvider);

    [Fact]
    public async Task Handle_WhenIdempotencyReturnsCachedResponse_ReturnsCachedWithoutExecuting()
    {
        // Arrange
        var handler = CreateHandler();
        var walletId = Guid.NewGuid();
        var cachedDto = new TransactionDto(
            Guid.NewGuid(), "TX-CACHED", "Credit", "Completed",
            Guid.NewGuid(), walletId, 100m, "USD", "idem-cached", null, null, 100m, DateTimeOffset.UtcNow);

        _idempotencyService.NextCheckResult = IdempotencyCheckResult<TransactionDto>.Processed(cachedDto);

        var command = new CreditWalletCommand(walletId, 100m, "USD", "idem-cached");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("TX-CACHED", result.TransactionCode);
        Assert.False(_unitOfWork.TransactionBegun);
    }

    [Fact]
    public async Task Handle_WhenWalletDoesNotExist_ThrowsDomainException()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new CreditWalletCommand(Guid.NewGuid(), 50m, "USD", "idem-key");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(ErrorCodes.WalletNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ExecutesCreditAndSavesIdempotency()
    {
        // Arrange
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var userWallet = Wallet.Create(userId, "USD", _dateTimeProvider.UtcNow);
        var treasuryWallet = Wallet.Create(Guid.NewGuid(), "USD", _dateTimeProvider.UtcNow);

        _walletRepository.Add(userWallet);
        _walletRepository.SetTreasuryWallet("USD", treasuryWallet);

        var command = new CreditWalletCommand(userWallet.Id, 75m, "USD", "idem-new");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(75m, result.Amount);
        Assert.Equal(75m, userWallet.Balance);
        Assert.True(_unitOfWork.TransactionCommitted);
        Assert.True(_idempotencyService.SaveCalled);
        Assert.Equal("idem-new", _idempotencyService.LastSavedKey);
    }

    private sealed class FakeIdempotencyService : IIdempotencyService
    {
        public IdempotencyCheckResult<TransactionDto> NextCheckResult { get; set; } = IdempotencyCheckResult<TransactionDto>.None();
        public bool SaveCalled { get; private set; }
        public string? LastSavedKey { get; private set; }

        public string ResolveKey(string? headerKey, string action, Guid walletId, string? referenceId, decimal? amount = null) =>
            headerKey ?? referenceId ?? "auto-key";

        public void CheckAntiDoubleClick(Guid walletId, decimal amount, string action, TimeSpan? window = null) { }

        public string ComputeHash<T>(T payload) => "computed-hash";

        public Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(string key, string endpoint, string hash, CancellationToken ct)
        {
            if (NextCheckResult.Result is TResult typed)
            {
                return Task.FromResult(IdempotencyCheckResult<TResult>.Processed(typed));
            }

            return Task.FromResult(IdempotencyCheckResult<TResult>.None());
        }

        public Task SaveAsync<TResult>(string key, string endpoint, string hash, TResult response, int statusCode, CancellationToken ct)
        {
            SaveCalled = true;
            LastSavedKey = key;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();
        private readonly Dictionary<string, Wallet> _treasury = new();

        public void Add(Wallet wallet) => _wallets[wallet.Id] = wallet;
        public void SetTreasuryWallet(string currency, Wallet wallet) => _treasury[currency.ToUpperInvariant()] = wallet;

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.GetValueOrDefault(id));

        public Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.GetValueOrDefault(id));

        public Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken cancellationToken = default) =>
            Task.FromResult(_treasury.GetValueOrDefault(currency.ToUpperInvariant()));

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        {
            Add(wallet);
            return Task.CompletedTask;
        }

        public void Update(Wallet wallet) { }
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.ContainsKey(id));
        public Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Wallet>>(_wallets.Values.Where(w => w.UserId == userId).ToList());
        public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.Values.FirstOrDefault(w => w.UserId == userId && w.Currency == currency));
    }

    private sealed class FakeTransactionRepository : ITransactionRepository
    {
        public List<Transaction> Transactions { get; } = new();

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

        public Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.IdempotencyKey == key));

        public Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Transaction>>(Transactions.Where(t => t.SourceWalletId == walletId || t.DestinationWalletId == walletId).ToList());
    }

    private sealed class FakeLedgerRepository : ILedgerRepository
    {
        public List<LedgerEntry> Entries { get; } = new();

        public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
        {
            Entries.AddRange(entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<LedgerEntry>>(Entries.Where(e => e.TransactionId == transactionId).ToList());

        public Task<IReadOnlyCollection<LedgerEntry>> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<LedgerEntry>>(Entries.Where(e => e.WalletId == walletId).ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool TransactionBegun { get; private set; }
        public bool TransactionCommitted { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            TransactionBegun = true;
            return Task.CompletedTask;
        }
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            TransactionCommitted = true;
            return Task.CompletedTask;
        }
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public FakeDateTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}
