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

    public void Credit(decimal amount, DateTimeOffset timestamp)
    {
        if (amount <= 0)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Credit amount must be greater than zero.");
        }

        if (Status != WalletStatus.Active)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Cannot credit an inactive wallet.");
        }

        Balance += amount;
        UpdatedAt = timestamp;
    }

    public void Debit(decimal amount, DateTimeOffset timestamp)
    {
        if (amount <= 0)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Debit amount must be greater than zero.");
        }

        if (Status != WalletStatus.Active)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Cannot debit an inactive wallet.");
        }

        if (Balance < amount)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Insufficient balance to debit.");
        }

        Balance -= amount;
        UpdatedAt = timestamp;
    }
}
