namespace PaymentLedger.Shared.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string Topic { get; set; } = "payment-ledger.events";

    public string GroupId { get; set; } = "payment-ledger-group";

    public bool Enabled { get; set; } = true;
}
