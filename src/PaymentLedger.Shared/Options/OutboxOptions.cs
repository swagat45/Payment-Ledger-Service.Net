namespace PaymentLedger.Shared.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 50;

    public int LockDurationSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 5;
}
