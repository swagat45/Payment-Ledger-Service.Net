namespace PaymentLedger.Shared.Context;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    IReadOnlyCollection<string> Scopes { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool HasScope(string scope);
}
