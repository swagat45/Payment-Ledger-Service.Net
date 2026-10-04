using PaymentLedger.Business.Models;

namespace PaymentLedger.Business.Services;

public interface IJwtTokenService
{
    TokenResponse GenerateToken(Guid userId, string email, string role, IReadOnlyCollection<string> scopes);
}
