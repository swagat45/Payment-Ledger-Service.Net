using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;
using StackExchange.Redis;

namespace PaymentLedger.Business.Common;

public sealed class IdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly TimeSpan DefaultThrottleWindow = TimeSpan.FromSeconds(3);

    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IIdempotencyRepository idempotencyRepository,
        IEnumerable<IConnectionMultiplexer> redisProviders,
        IDateTimeProvider dateTimeProvider,
        ILogger<IdempotencyService> logger)
    {
        _idempotencyRepository = idempotencyRepository;
        _redis = redisProviders.FirstOrDefault();
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    private IDatabase? GetRedisDatabase() => _redis?.GetDatabase();

    public string ResolveKey(string? headerKey, string action, Guid walletId, string? referenceId, decimal? amount = null)
    {
        if (!string.IsNullOrWhiteSpace(headerKey))
        {
            return headerKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            return $"{action.Trim().ToLowerInvariant()}:{walletId:D}:{referenceId.Trim()}";
        }

        if (amount.HasValue)
        {
            CheckAntiDoubleClick(walletId, amount.Value, action);
        }

        return $"auto:{Guid.NewGuid():N}";
    }

    public void CheckAntiDoubleClick(Guid walletId, decimal amount, string action, TimeSpan? window = null)
    {
        var duration = window ?? DefaultThrottleWindow;
        var normalizedAmount = decimal.Round(amount, 2).ToString("0.00");
        var rawKey = $"{action.Trim().ToLowerInvariant()}:{walletId:D}:{normalizedAmount}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        var cacheKey = $"anti-double-click:{hash}";

        var db = GetRedisDatabase();
        if (db != null)
        {
            try
            {
                var acquired = db.StringSet(cacheKey, "1", duration, When.NotExists);
                if (!acquired)
                {
                    _logger.LogWarning(
                        "Duplicate request detected for action '{Action}' on wallet '{WalletId}' with amount '{Amount}' within throttle window (Redis locked).",
                        action,
                        walletId,
                        amount);

                    throw new DomainException("Duplicate request detected. Please wait before retrying.", ErrorCodes.DuplicateRequest);
                }
            }
            catch (DomainException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis anti-double-click check failed due to connectivity issue for key '{CacheKey}'. Falling back gracefully.", cacheKey);
            }
        }
        else
        {
            // Fallback (for tests or missing redis)
            _logger.LogWarning("Redis is not configured. Anti-double-click guard is disabled for key {CacheKey}", cacheKey);
        }
    }

    public string ComputeHash<T>(T payload)
    {
        if (payload == null)
        {
            return string.Empty;
        }

        if (payload is string stringPayload)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stringPayload));
            return Convert.ToHexStringLower(bytes);
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hashBytes);
    }

    private record CachedIdempotencyRecord(string RequestHash, string ResponseBody);

    public async Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(
        string key,
        string endpoint,
        string hash,
        CancellationToken ct)
    {
        var db = GetRedisDatabase();
        var cacheKey = $"idemp:{key}:{endpoint}";

        if (db != null)
        {
            try
            {
                var cachedVal = await db.StringGetAsync(cacheKey);
                if (cachedVal.HasValue)
                {
                    var cachedRec = JsonSerializer.Deserialize<CachedIdempotencyRecord>(cachedVal.ToString(), JsonOptions);
                    if (cachedRec != null)
                    {
                        if (!string.Equals(cachedRec.RequestHash, hash, StringComparison.Ordinal))
                        {
                            _logger.LogWarning(
                                "Idempotency conflict for key '{Key}' on endpoint '{Endpoint}' from Redis. Existing hash '{ExistingHash}' does not match incoming hash '{Hash}'.",
                                key, endpoint, cachedRec.RequestHash, hash);

                            throw new DomainException(
                                "Idempotency key was previously used with a different request payload.",
                                ErrorCodes.IdempotencyConflict);
                        }
                        var cachedResult = JsonSerializer.Deserialize<TResult>(cachedRec.ResponseBody, JsonOptions);
                        if (cachedResult != null)
                        {
                            return IdempotencyCheckResult<TResult>.Processed(cachedResult);
                        }
                    }
                }
            }
            catch (DomainException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis idempotency cache read failed for key '{Key}' on endpoint '{Endpoint}'. Falling back to database.", key, endpoint);
            }
        }

        var existing = await _idempotencyRepository.GetByKeyAndEndpointAsync(key, endpoint, ct);
        if (existing == null)
        {
            return IdempotencyCheckResult<TResult>.None();
        }

        if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Idempotency conflict for key '{Key}' on endpoint '{Endpoint}' from DB. Existing hash '{ExistingHash}' does not match incoming hash '{Hash}'.",
                key,
                endpoint,
                existing.RequestHash,
                hash);

            throw new DomainException(
                "Idempotency key was previously used with a different request payload.",
                ErrorCodes.IdempotencyConflict);
        }

        if (!string.IsNullOrWhiteSpace(existing.ResponseBody))
        {
            // Populate Redis cache for future reads
            if (db != null)
            {
                try
                {
                    var cacheRec = new CachedIdempotencyRecord(existing.RequestHash, existing.ResponseBody);
                    var cacheVal = JsonSerializer.Serialize(cacheRec, JsonOptions);
                    await db.StringSetAsync(cacheKey, cacheVal, TimeSpan.FromHours(24));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to populate Redis cache for key '{Key}' on endpoint '{Endpoint}'.", key, endpoint);
                }
            }

            var cached = JsonSerializer.Deserialize<TResult>(existing.ResponseBody, JsonOptions);
            if (cached != null)
            {
                return IdempotencyCheckResult<TResult>.Processed(cached);
            }
        }

        throw new DomainException(
            "A request with this idempotency key is currently being processed.",
            ErrorCodes.ConcurrencyConflict);
    }

    public async Task SaveAsync<TResult>(
        string key,
        string endpoint,
        string hash,
        TResult response,
        int statusCode,
        CancellationToken ct)
    {
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        var now = _dateTimeProvider.UtcNow;

        var record = IdempotencyRequest.Create(
            idempotencyKey: key,
            endpoint: endpoint,
            requestHash: hash,
            responseBody: responseJson,
            statusCode: statusCode,
            status: "Completed",
            createdAt: now,
            expiresAt: now.AddHours(24));

        await _idempotencyRepository.AddAsync(record, ct);

        var db = GetRedisDatabase();
        if (db != null)
        {
            try
            {
                var cacheKey = $"idemp:{key}:{endpoint}";
                var cacheRec = new CachedIdempotencyRecord(hash, responseJson);
                var cacheVal = JsonSerializer.Serialize(cacheRec, JsonOptions);
                await db.StringSetAsync(cacheKey, cacheVal, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write completed response to Redis cache for key '{Key}' on endpoint '{Endpoint}'.", key, endpoint);
            }
        }
    }
}

