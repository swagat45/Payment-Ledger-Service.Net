using System.Security.Claims;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Context;

namespace PaymentLedger.Api.Common;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userIdClaim = User?.FindFirstValue(SecurityConstants.ClaimTypes.UserId) ??
                              User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return null;
        }
    }

    public string? Email => User?.FindFirstValue(SecurityConstants.ClaimTypes.Email) ??
                            User?.FindFirstValue(ClaimTypes.Email);

    public string? Role => User?.FindFirstValue(SecurityConstants.ClaimTypes.Role) ??
                           User?.FindFirstValue(ClaimTypes.Role);

    public IReadOnlyCollection<string> Scopes
    {
        get
        {
            return User?.FindAll(SecurityConstants.ClaimTypes.Scope)
                       .Select(c => c.Value)
                       .ToList() ?? new List<string>();
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => User?.IsInRole(SecurityConstants.Roles.Admin) ?? false;

    public bool HasScope(string scope)
    {
        return Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}
