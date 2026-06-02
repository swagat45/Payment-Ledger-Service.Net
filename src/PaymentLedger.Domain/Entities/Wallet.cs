using PaymentLedger.Domain.Enums;

namespace PaymentLedger.Domain.Entities;

public class Wallet
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public WalletStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

}
