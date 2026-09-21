using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Transactions.Common;
using PaymentLedger.Application.Wallets.Commands.DebitWallet;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Infrastructure.Common;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Application.Wallets;

/// <summary>
/// Concurrency simulation tests validating that row-level locking (SELECT ... FOR UPDATE)
/// prevents race conditions, enforces strict serialization, ensures wallet balance never goes negative,
/// and validates idempotency deduplication under heavy parallel execution.
/// </summary>
public class WalletConcurrencySimulationTests
{
    [Fact]
    public async Task ConcurrentDebits_OnLowBalanceWallet_SerializesAndPreventsNegativeBalance()
    {
        // Arrange
        // Initial balance: 100 USD.
        // 10 concurrent requests each attempting to debit 30 USD (Total requested: 300 USD).
        // Max possible successful debits = 3 (100 - 30*3 = 10 USD remaining).
        // 7 requests MUST be rejected with InsufficientBalance.
        // Balance MUST NEVER drop below zero.

        var fixture = new ConcurrencySimulationFixture();
        var walletId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var initialWallet = Wallet.Create(userId, "USD", fixture.DateTimeProvider.UtcNow);
        initialWallet.Credit(100m, fixture.DateTimeProvider.UtcNow);
        fixture.WalletStore[walletId] = initialWallet;

        var treasuryWallet = Wallet.Create(Guid.NewGuid(), "USD", fixture.DateTimeProvider.UtcNow);
        fixture.TreasuryStore["USD"] = treasuryWallet;

        const int totalTasks = 10;
        const decimal debitAmount = 30m;

        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successResults = new ConcurrentBag<TransactionDto>();
        var insufficientBalanceExceptions = new ConcurrentBag<DomainException>();
        var unexpectedExceptions = new ConcurrentBag<Exception>();

        // Act - create 10 concurrent debit operations
        var tasks = Enumerable.Range(1, totalTasks).Select(async i =>
        {
            await startSignal.Task;

            // Small randomized jitter to maximize thread interleaving
            await Task.Yield();

            var uow = fixture.CreateUnitOfWork();
            var handler = new DebitWalletCommandHandler(
                fixture.CreateWalletRepository(uow),
                fixture.TransactionRepository,
                fixture.LedgerRepository,
                fixture.IdempotencyService,
                uow,
                fixture.DateTimeProvider);

            var command = new DebitWalletCommand(
                walletId,
                debitAmount,
                "USD",
                $"concurrent-idem-key-{i}",
                Description: $"Concurrent debit {i}");

            try
            {
                var result = await handler.Handle(command, CancellationToken.None);
                successResults.Add(result);
            }
            catch (DomainException ex) when (ex.ErrorCode == ErrorCodes.InsufficientBalance)
            {
                insufficientBalanceExceptions.Add(ex);
            }
            catch (Exception ex)
            {
                unexpectedExceptions.Add(ex);
            }
        });

        var allTasks = Task.WhenAll(tasks);
        startSignal.SetResult();
        await allTasks;

        // Assert
        Assert.Empty(unexpectedExceptions);
        Assert.Equal(3, successResults.Count);
        Assert.Equal(7, insufficientBalanceExceptions.Count);

        // Final balance check: exactly 10 USD (100 - 30 * 3)
        var finalWallet = fixture.WalletStore[walletId];
        Assert.Equal(10m, finalWallet.Balance);

        // Invariant: Balance was never negative
        Assert.True(fixture.MinimumObservedBalance >= 0, $"Balance dropped to {fixture.MinimumObservedBalance}!");

        // Transactions recorded in ledger: exactly 3 debits of 30 USD
        Assert.Equal(3, fixture.TransactionRepository.Transactions.Count);
        Assert.All(fixture.TransactionRepository.Transactions, tx =>
        {
            Assert.Equal(30m, tx.Amount);
            Assert.Equal("USD", tx.Currency);
        });

        // Double-entry ledger entries: 3 user debits + 3 treasury credits = 6 entries
        Assert.Equal(6, fixture.LedgerRepository.Entries.Count);
    }

    [Fact]
    public async Task ConcurrentDebits_WithIdenticalIdempotencyKey_ExecutesDebitOnlyOnce()
    {
        // Arrange
        // 10 concurrent requests submitting the EXACT SAME idempotency key and amount.
        // Only 1 execution should mutate the wallet. All others must receive the cached response
        // or be throttled by the anti-double-click guard (429).
        // Balance must decrease by only 30 USD (from 100 to 70), NEVER debited multiple times.

        var fixture = new ConcurrencySimulationFixture();
        var walletId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var initialWallet = Wallet.Create(userId, "USD", fixture.DateTimeProvider.UtcNow);
        initialWallet.Credit(100m, fixture.DateTimeProvider.UtcNow);
        fixture.WalletStore[walletId] = initialWallet;

        var treasuryWallet = Wallet.Create(Guid.NewGuid(), "USD", fixture.DateTimeProvider.UtcNow);
        fixture.TreasuryStore["USD"] = treasuryWallet;

        const int totalTasks = 10;
        const decimal debitAmount = 30m;
        const string sharedIdempotencyKey = "shared-duplicate-idem-key";

        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successfulResponses = new ConcurrentBag<TransactionDto>();
        var duplicateThrottledCount = 0;
        var unexpectedExceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(1, totalTasks).Select(async _ =>
        {
            await startSignal.Task;
            await Task.Yield();

            var uow = fixture.CreateUnitOfWork();
            var handler = new DebitWalletCommandHandler(
                fixture.CreateWalletRepository(uow),
                fixture.TransactionRepository,
                fixture.LedgerRepository,
                fixture.IdempotencyService,
                uow,
                fixture.DateTimeProvider);

            var command = new DebitWalletCommand(
                walletId,
                debitAmount,
                "USD",
                sharedIdempotencyKey,
                Description: "Duplicate concurrent debit");

            try
            {
                var result = await handler.Handle(command, CancellationToken.None);
                successfulResponses.Add(result);
            }
            catch (DomainException ex) when (ex.ErrorCode == ErrorCodes.DuplicateRequest)
            {
                Interlocked.Increment(ref duplicateThrottledCount);
            }
            catch (Exception ex)
            {
                unexpectedExceptions.Add(ex);
            }
        });

        var allTasks = Task.WhenAll(tasks);
        startSignal.SetResult();
        await allTasks;

        // Assert
        Assert.Empty(unexpectedExceptions);

        // All returned transaction dtos must have the same transaction ID and code
        var firstTx = successfulResponses.FirstOrDefault();
        if (firstTx != null)
        {
            Assert.All(successfulResponses, tx =>
            {
                Assert.Equal(firstTx.TransactionId, tx.TransactionId);
                Assert.Equal(firstTx.TransactionCode, tx.TransactionCode);
            });
        }

        // The wallet must have been debited EXACTLY ONCE
        var finalWallet = fixture.WalletStore[walletId];
        Assert.Equal(70m, finalWallet.Balance);

        // Exactly 1 debit transaction must exist
        Assert.Single(fixture.TransactionRepository.Transactions);

        // Exactly 2 ledger entries (1 user debit, 1 treasury credit)
        Assert.Equal(2, fixture.LedgerRepository.Entries.Count);
    }

    #region Simulation Fixture & Fakes

    private sealed class ConcurrencySimulationFixture
    {
        public ConcurrentDictionary<Guid, Wallet> WalletStore { get; } = new();
        public ConcurrentDictionary<string, Wallet> TreasuryStore { get; } = new();
        public ConcurrentDictionary<Guid, SemaphoreSlim> RowLocks { get; } = new();

        public SimulatedTransactionRepository TransactionRepository { get; } = new();
        public SimulatedLedgerRepository LedgerRepository { get; } = new();
        public SimulatedIdempotencyRepository IdempotencyRepository { get; } = new();
        public IIdempotencyService IdempotencyService { get; }
        public IDateTimeProvider DateTimeProvider { get; } = new SimulatedDateTimeProvider();

        private decimal _minObservedBalance = decimal.MaxValue;
        private readonly object _balanceLock = new();

        public decimal MinimumObservedBalance
        {
            get
            {
                lock (_balanceLock)
                {
                    return _minObservedBalance;
                }
            }
        }

        public void RecordBalanceObservation(decimal balance)
        {
            lock (_balanceLock)
            {
                if (balance < _minObservedBalance)
                {
                    _minObservedBalance = balance;
                }
            }
        }

        public ConcurrencySimulationFixture()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IdempotencyService = new IdempotencyService(
                IdempotencyRepository,
                memoryCache,
                DateTimeProvider,
                NullLogger<IdempotencyService>.Instance);
        }

        public SimulatedUnitOfWork CreateUnitOfWork() => new(this);

        public IWalletRepository CreateWalletRepository(SimulatedUnitOfWork uow) =>
            new SimulatedWalletRepository(this, uow);
    }

    private sealed class SimulatedUnitOfWork : IUnitOfWork
    {
        private readonly ConcurrencySimulationFixture _fixture;
        private readonly List<SemaphoreSlim> _acquiredLocks = new();
        private readonly object _lockObj = new();

        public SimulatedUnitOfWork(ConcurrencySimulationFixture fixture)
        {
            _fixture = fixture;
        }

        public void RegisterLock(SemaphoreSlim sem)
        {
            lock (_lockObj)
            {
                _acquiredLocks.Add(sem);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            ReleaseLocks();
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            ReleaseLocks();
            return Task.CompletedTask;
        }

        private void ReleaseLocks()
        {
            lock (_lockObj)
            {
                foreach (var sem in _acquiredLocks)
                {
                    sem.Release();
                }
                _acquiredLocks.Clear();
            }
        }
    }

    private sealed class SimulatedWalletRepository : IWalletRepository
    {
        private readonly ConcurrencySimulationFixture _fixture;
        private readonly SimulatedUnitOfWork _uow;

        public SimulatedWalletRepository(ConcurrencySimulationFixture fixture, SimulatedUnitOfWork uow)
        {
            _fixture = fixture;
            _uow = uow;
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        {
            _fixture.WalletStore[wallet.Id] = wallet;
            return Task.CompletedTask;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _fixture.WalletStore.TryGetValue(id, out var wallet);
            return Task.FromResult(wallet);
        }

        public async Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Simulates PostgreSQL SELECT ... FOR UPDATE row-level exclusive lock
            var sem = _fixture.RowLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(cancellationToken);
            _uow.RegisterLock(sem);

            if (_fixture.WalletStore.TryGetValue(id, out var wallet))
            {
                _fixture.RecordBalanceObservation(wallet.Balance);
                return wallet;
            }

            return null;
        }

        public Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken cancellationToken = default)
        {
            _fixture.TreasuryStore.TryGetValue(currency.ToUpperInvariant(), out var wallet);
            return Task.FromResult(wallet);
        }

        public void Update(Wallet wallet)
        {
            _fixture.RecordBalanceObservation(wallet.Balance);
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_fixture.WalletStore.ContainsKey(id));

        public Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Wallet>>(_fixture.WalletStore.Values.Where(w => w.UserId == userId).ToList());

        public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default) =>
            Task.FromResult(_fixture.WalletStore.Values.FirstOrDefault(w => w.UserId == userId && w.Currency == currency));
    }

    private sealed class SimulatedTransactionRepository : ITransactionRepository
    {
        public ConcurrentBag<Transaction> Transactions { get; } = new();

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

    private sealed class SimulatedLedgerRepository : ILedgerRepository
    {
        public ConcurrentBag<LedgerEntry> Entries { get; } = new();

        public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
        {
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<LedgerEntry>>(Entries.Where(e => e.TransactionId == transactionId).ToList());

        public Task<IReadOnlyCollection<LedgerEntry>> GetByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<LedgerEntry>>(Entries.Where(e => e.WalletId == walletId).ToList());
    }

    private sealed class SimulatedIdempotencyRepository : IIdempotencyRepository
    {
        private readonly ConcurrentDictionary<string, IdempotencyRequest> _store = new();

        public Task AddAsync(IdempotencyRequest request, CancellationToken cancellationToken = default)
        {
            var key = $"{request.IdempotencyKey}:{request.Endpoint}";
            _store[key] = request;
            return Task.CompletedTask;
        }

        public Task<IdempotencyRequest?> GetByKeyAndEndpointAsync(string idempotencyKey, string endpoint, CancellationToken cancellationToken = default)
        {
            var key = $"{idempotencyKey}:{endpoint}";
            _store.TryGetValue(key, out var request);
            return Task.FromResult(request);
        }
    }

    private sealed class SimulatedDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    #endregion
}
