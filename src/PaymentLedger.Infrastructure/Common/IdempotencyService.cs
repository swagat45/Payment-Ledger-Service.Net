using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.Infrastructure.Common;

public sealed class IdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly TimeSpan DefaultThrottleWindow = TimeSpan.FromSeconds(3);
    private static readonly object AntiDoubleClickLock = new();

    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IIdempotencyRepository idempotencyRepository,
        IMemoryCache memoryCache,
        IDateTimeProvider dateTimeProvider,
        ILogger<IdempotencyService> logger)
    {
        _idempotencyRepository = idempotencyRepository;
        _memoryCache = memoryCache;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

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

        lock (AntiDoubleClickLock)
        {
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning(
                    "Duplicate request detected for action '{Action}' on wallet '{WalletId}' with amount '{Amount}' within throttle window.",
                    action,
                    walletId,
                    amount);

                throw new DomainException("Duplicate request detected. Please wait before retrying.", ErrorCodes.DuplicateRequest);
            }

            _memoryCache.Set(cacheKey, true, duration);
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

    public async Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(
        string key,
        string endpoint,
        string hash,
        CancellationToken ct)
    {
        var existing = await _idempotencyRepository.GetByKeyAndEndpointAsync(key, endpoint, ct);
        if (existing == null)
        {
            return IdempotencyCheckResult<TResult>.None();
        }

        if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Idempotency conflict for key '{Key}' on endpoint '{Endpoint}'. Existing hash '{ExistingHash}' does not match incoming hash '{Hash}'.",
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
    }
}
