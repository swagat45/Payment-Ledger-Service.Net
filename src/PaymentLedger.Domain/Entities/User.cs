using PaymentLedger.Domain.Enums;

namespace PaymentLedger.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public User()
    {
    }

    public static User Create(string fullName, string email, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new Exceptions.DomainException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new Exceptions.DomainException("Email is required.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Status = UserStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Update(string fullName, string email, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new Exceptions.DomainException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new Exceptions.DomainException("Email is required.");
        }

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        Status = UserStatus.Inactive;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTimeOffset utcNow)
    {
        Status = UserStatus.Active;
        UpdatedAt = utcNow;
    }
}
