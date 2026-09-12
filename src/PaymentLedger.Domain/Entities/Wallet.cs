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

    public Wallet()
    {
    }

    public static Wallet Create(Guid userId, string currency, DateTimeOffset utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("User ID is required.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Currency is required.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsLetter))
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Currency must be a valid 3-letter code.");
        }

        return new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Currency = normalizedCurrency,
            Balance = 0m,
            Status = WalletStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Block(DateTimeOffset timestamp)
    {
        if (Status == WalletStatus.Closed)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Cannot block a closed wallet.");
        }

        Status = WalletStatus.Blocked;
        UpdatedAt = timestamp;
    }

    public void Unblock(DateTimeOffset timestamp)
    {
        if (Status == WalletStatus.Closed)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Cannot unblock a closed wallet.");
        }

        Status = WalletStatus.Active;
        UpdatedAt = timestamp;
    }

    public void Close(DateTimeOffset timestamp)
    {
        if (Status == WalletStatus.Closed)
        {
            return;
        }

        if (Balance != 0)
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Cannot close a wallet with a non-zero balance.");
        }

        Status = WalletStatus.Closed;
        UpdatedAt = timestamp;
    }

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
