using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace PaymentLedger.Api.Common;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IConnectionMultiplexer> _redisProviders;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IEnumerable<IConnectionMultiplexer> redisProviders,
        ILogger<RedisHealthCheck> logger)
    {
        _redisProviders = redisProviders;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var redis = _redisProviders.FirstOrDefault();
        if (redis == null)
        {
            return HealthCheckResult.Healthy("Redis is not registered (running in fallback mode).");
        }

        try
        {
            var db = redis.GetDatabase();
            var pingLatency = await db.PingAsync();
            return HealthCheckResult.Healthy($"Redis is operational (Ping: {pingLatency.TotalMilliseconds:F1}ms).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis health check probe failed.");
            return HealthCheckResult.Degraded($"Redis is degraded or unreachable: {ex.Message}");
        }
    }
}
