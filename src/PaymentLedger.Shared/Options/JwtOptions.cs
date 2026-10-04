namespace PaymentLedger.Shared.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "PaymentLedger";

    public string Audience { get; set; } = "PaymentLedger.Clients";

    public string SecretKey { get; set; } = "PaymentLedger_SuperSecret_Jwt_SigningKey_2026_Min256BitsLongKey!";

    public int ExpiryMinutes { get; set; } = 60;
}
