namespace PaymentLedger.Infrastructure.Options;

public sealed class TreasuryWalletOptions
{
    public const string SectionName = "TreasuryWallet";

    public string UserEmail { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Currencies { get; set; } = Array.Empty<string>();
}
