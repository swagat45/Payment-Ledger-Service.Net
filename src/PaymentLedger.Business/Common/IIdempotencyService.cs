namespace PaymentLedger.Business.Common;

public interface IIdempotencyService
{
    string ResolveKey(string? headerKey, string action, Guid walletId, string? referenceId, decimal? amount = null);

    void CheckAntiDoubleClick(Guid walletId, decimal amount, string action, TimeSpan? window = null);

    string ComputeHash<T>(T payload);

    Task<IdempotencyCheckResult<TResult>> CheckAsync<TResult>(
        string key,
        string endpoint,
        string hash,
        CancellationToken ct);

    Task SaveAsync<TResult>(
        string key,
        string endpoint,
        string hash,
        TResult response,
        int statusCode,
        CancellationToken ct);
}

public sealed record IdempotencyCheckResult<TResult>(
    bool IsProcessed,
    TResult? Result = default,
    bool IsPending = false,
    bool IsMismatch = false)
{
    public static IdempotencyCheckResult<TResult> None() => new(false);

    public static IdempotencyCheckResult<TResult> Processed(TResult result) => new(true, result);

    public static IdempotencyCheckResult<TResult> Pending() => new(false, default, IsPending: true);

    public static IdempotencyCheckResult<TResult> Mismatch() => new(false, default, IsMismatch: true);
}
