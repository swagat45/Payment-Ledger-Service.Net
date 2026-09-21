using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Infrastructure.Common;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Infrastructure;

public class IdempotencyServiceTests
{
    private readonly FakeIdempotencyRepository _repository = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);

    private IdempotencyService CreateService()
    {
        return new IdempotencyService(
            _repository,
            _memoryCache,
            _dateTimeProvider,
            NullLogger<IdempotencyService>.Instance);
    }

    [Fact]
    public void ResolveKey_WhenHeaderKeyProvided_ReturnsHeaderKeyTrimmed()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();

        // Act
        var result = service.ResolveKey("  header-key-123  ", "credit", walletId, "ref-456");

        // Assert
        Assert.Equal("header-key-123", result);
    }

    [Fact]
    public void ResolveKey_WhenHeaderOmittedAndReferenceProvided_ReturnsDeterministicKey()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();

        // Act
        var result = service.ResolveKey(null, "credit", walletId, "order-789");

        // Assert
        Assert.Equal($"credit:{walletId:D}:order-789", result);
    }

    [Fact]
    public void ResolveKey_WhenNeitherProvided_GeneratesAutoKey()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();

        // Act
        var result = service.ResolveKey(null, "credit", walletId, null);

        // Assert
        Assert.StartsWith("auto:", result);
    }

    [Fact]
    public void CheckAntiDoubleClick_WhenSameOperationWithinWindow_ThrowsDuplicateRequest()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();
        const decimal amount = 100m;
        const string action = "credit";

        // First call should succeed and record fingerprint in cache
        service.CheckAntiDoubleClick(walletId, amount, action, TimeSpan.FromSeconds(5));

        // Act & Assert - second immediate call should throw
        var ex = Assert.Throws<DomainException>(() =>
            service.CheckAntiDoubleClick(walletId, amount, action, TimeSpan.FromSeconds(5)));

        Assert.Equal(ErrorCodes.DuplicateRequest, ex.ErrorCode);
        Assert.Equal("Duplicate request detected. Please wait before retrying.", ex.Message);
    }

    [Fact]
    public void CheckAntiDoubleClick_WithDifferentAmountOrAction_Passes()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();

        // Act & Assert (none of these should throw)
        service.CheckAntiDoubleClick(walletId, 50m, "credit");
        service.CheckAntiDoubleClick(walletId, 75m, "credit");
        service.CheckAntiDoubleClick(walletId, 50m, "debit");
    }

    [Fact]
    public void ComputeHash_WithSamePayload_ReturnsIdenticalHash()
    {
        // Arrange
        var service = CreateService();
        var payload1 = new { WalletId = Guid.NewGuid(), Amount = 100.50m, Currency = "USD" };
        var payload2 = new { payload1.WalletId, payload1.Amount, payload1.Currency };

        // Act
        var hash1 = service.ComputeHash(payload1);
        var hash2 = service.ComputeHash(payload2);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_WithDifferentPayload_ReturnsDifferentHash()
    {
        // Arrange
        var service = CreateService();
        var payload1 = new { Amount = 100m, Currency = "USD" };
        var payload2 = new { Amount = 200m, Currency = "USD" };

        // Act
        var hash1 = service.ComputeHash(payload1);
        var hash2 = service.ComputeHash(payload2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordDoesNotExist_ReturnsNotProcessed()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.CheckAsync<string>(
            "non-existent-key",
            "/api/v1/wallets/test",
            "hash123",
            CancellationToken.None);

        // Assert
        Assert.False(result.IsProcessed);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordExistsWithMatchingHashAndBody_ReturnsCachedResult()
    {
        // Arrange
        var service = CreateService();
        const string key = "test-key-1";
        const string endpoint = "/api/v1/wallets/credit";
        const string hash = "matching-hash";
        var sampleResponse = new TestDto("ORD123", 100m);

        var existing = IdempotencyRequest.Create(
            key,
            endpoint,
            hash,
            JsonSerializer.Serialize(sampleResponse),
            200,
            "Completed",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(24));

        await _repository.AddAsync(existing);

        // Act
        var result = await service.CheckAsync<TestDto>(key, endpoint, hash, CancellationToken.None);

        // Assert
        Assert.True(result.IsProcessed);
        Assert.NotNull(result.Result);
        Assert.Equal("ORD123", result.Result.Code);
        Assert.Equal(100m, result.Result.Amount);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordExistsWithDifferentHash_ThrowsIdempotencyConflict()
    {
        // Arrange
        var service = CreateService();
        const string key = "test-key-2";
        const string endpoint = "/api/v1/wallets/credit";

        var existing = IdempotencyRequest.Create(
            key,
            endpoint,
            "first-hash",
            "{}",
            200,
            "Completed",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(24));

        await _repository.AddAsync(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CheckAsync<TestDto>(key, endpoint, "different-hash", CancellationToken.None));

        Assert.Equal(ErrorCodes.IdempotencyConflict, ex.ErrorCode);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordExistsWithoutResponse_ThrowsConcurrencyConflict()
    {
        // Arrange
        var service = CreateService();
        const string key = "test-key-3";
        const string endpoint = "/api/v1/wallets/credit";
        const string hash = "same-hash";

        var existing = IdempotencyRequest.Create(
            key,
            endpoint,
            hash,
            null,
            null,
            "Processing",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(24));

        await _repository.AddAsync(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CheckAsync<TestDto>(key, endpoint, hash, CancellationToken.None));

        Assert.Equal(ErrorCodes.ConcurrencyConflict, ex.ErrorCode);
    }

    [Fact]
    public async Task SaveAsync_PersistsIdempotencyRecord()
    {
        // Arrange
        var service = CreateService();
        const string key = "saved-key";
        const string endpoint = "/api/v1/wallets/save";
        const string hash = "saved-hash";
        var sample = new TestDto("TEST", 50m);

        // Act
        await service.SaveAsync(key, endpoint, hash, sample, 200, CancellationToken.None);

        // Assert
        var saved = await _repository.GetByKeyAndEndpointAsync(key, endpoint);
        Assert.NotNull(saved);
        Assert.Equal(key, saved.IdempotencyKey);
        Assert.Equal(endpoint, saved.Endpoint);
        Assert.Equal(hash, saved.RequestHash);
        Assert.Equal(200, saved.StatusCode);
        Assert.Equal("Completed", saved.Status);
    }

    [Fact]
    public async Task CheckAntiDoubleClick_WhenConcurrentThreadsAttemptSameOperation_AllowsOnlyOneAndRejectsOthers()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();
        const decimal amount = 150m;
        const string action = "debit";

        const int concurrencyLevel = 20;
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successCount = 0;
        var duplicateCount = 0;

        // Act
        var tasks = Enumerable.Range(0, concurrencyLevel).Select(async _ =>
        {
            await startSignal.Task;
            try
            {
                service.CheckAntiDoubleClick(walletId, amount, action, TimeSpan.FromSeconds(10));
                Interlocked.Increment(ref successCount);
            }
            catch (DomainException ex) when (ex.ErrorCode == ErrorCodes.DuplicateRequest)
            {
                Interlocked.Increment(ref duplicateCount);
            }
        });

        // Fire all threads simultaneously
        var allTasks = Task.WhenAll(tasks);
        startSignal.SetResult();
        await allTasks;

        // Assert
        Assert.Equal(1, successCount);
        Assert.Equal(concurrencyLevel - 1, duplicateCount);
    }

    [Fact]
    public async Task CheckAntiDoubleClick_WhenConcurrentThreadsTargetDifferentWallets_AllowsAll()
    {
        // Arrange
        var service = CreateService();
        const int concurrencyLevel = 20;
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successCount = 0;

        // Act
        var tasks = Enumerable.Range(0, concurrencyLevel).Select(async _ =>
        {
            var walletId = Guid.NewGuid();
            await startSignal.Task;
            service.CheckAntiDoubleClick(walletId, 50m, "credit", TimeSpan.FromSeconds(10));
            Interlocked.Increment(ref successCount);
        });

        var allTasks = Task.WhenAll(tasks);
        startSignal.SetResult();
        await allTasks;

        // Assert
        Assert.Equal(concurrencyLevel, successCount);
    }

    [Fact]
    public void ResolveKey_WhenReferenceIdHasWhitespace_TrimsCorrectly()
    {
        // Arrange
        var service = CreateService();
        var walletId = Guid.NewGuid();

        // Act
        var result = service.ResolveKey(null, "debit", walletId, "   order-ref-999   ");

        // Assert
        Assert.Equal($"debit:{walletId:D}:order-ref-999", result);
    }

    private sealed record TestDto(string Code, decimal Amount);

    private sealed class FakeIdempotencyRepository : IIdempotencyRepository
    {
        private readonly List<IdempotencyRequest> _records = new();

        public Task AddAsync(IdempotencyRequest request, CancellationToken cancellationToken = default)
        {
            _records.Add(request);
            return Task.CompletedTask;
        }

        public Task<IdempotencyRequest?> GetByKeyAndEndpointAsync(
            string idempotencyKey,
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            var found = _records.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey && r.Endpoint == endpoint);
            return Task.FromResult(found);
        }
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public FakeDateTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
