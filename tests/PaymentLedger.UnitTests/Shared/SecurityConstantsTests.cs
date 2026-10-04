using System.Text;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Context;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.UnitTests.Shared;

public class SecurityConstantsTests
{
    [Fact]
    public void SecurityConstants_Scopes_ContainsRequiredScopes()
    {
        Assert.Equal("read:wallet", SecurityConstants.Scopes.ReadWallet);
        Assert.Equal("write:wallet", SecurityConstants.Scopes.WriteWallet);
        Assert.Equal("admin:ledger", SecurityConstants.Scopes.AdminLedger);

        Assert.Contains(SecurityConstants.Scopes.ReadWallet, SecurityConstants.Scopes.AllScopes);
        Assert.Contains(SecurityConstants.Scopes.WriteWallet, SecurityConstants.Scopes.AllScopes);
        Assert.Contains(SecurityConstants.Scopes.AdminLedger, SecurityConstants.Scopes.AllScopes);
    }

    [Fact]
    public void SecurityConstants_Roles_ContainsRequiredRoles()
    {
        Assert.Equal("Admin", SecurityConstants.Roles.Admin);
        Assert.Equal("Customer", SecurityConstants.Roles.Customer);
        Assert.Equal("Service", SecurityConstants.Roles.Service);

        Assert.Contains(SecurityConstants.Roles.Admin, SecurityConstants.Roles.AllRoles);
        Assert.Contains(SecurityConstants.Roles.Customer, SecurityConstants.Roles.AllRoles);
        Assert.Contains(SecurityConstants.Roles.Service, SecurityConstants.Roles.AllRoles);
    }

    [Fact]
    public void SecurityConstants_Policies_MatchesPolicyNames()
    {
        Assert.Equal("ReadWallet", SecurityConstants.Policies.ReadWallet);
        Assert.Equal("WriteWallet", SecurityConstants.Policies.WriteWallet);
        Assert.Equal("AdminLedger", SecurityConstants.Policies.AdminLedger);
    }

    [Fact]
    public void ErrorCodes_ContainsSecurityErrorCodes()
    {
        Assert.Equal("UNAUTHORIZED", ErrorCodes.Unauthorized);
        Assert.Equal("FORBIDDEN", ErrorCodes.Forbidden);
        Assert.Equal("INVALID_CREDENTIALS", ErrorCodes.InvalidCredentials);
        Assert.Equal("INVALID_TOKEN", ErrorCodes.InvalidToken);
    }

    [Fact]
    public void JwtOptions_HasSecureDefaults()
    {
        var options = new JwtOptions();

        Assert.Equal("PaymentLedger", options.Issuer);
        Assert.Equal("PaymentLedger.Clients", options.Audience);
        Assert.True(options.ExpiryMinutes > 0);
        Assert.True(Encoding.UTF8.GetByteCount(options.SecretKey) >= 32, "JWT secret key must be at least 256 bits (32 bytes).");
    }

    [Fact]
    public void ICurrentUserService_MockContract_FunctionsAsExpected()
    {
        var userId = Guid.NewGuid();
        var mockUser = new TestCurrentUserService(
            userId: userId,
            email: "test@example.com",
            role: SecurityConstants.Roles.Customer,
            scopes: [SecurityConstants.Scopes.ReadWallet],
            isAuthenticated: true);

        Assert.Equal(userId, mockUser.UserId);
        Assert.Equal("test@example.com", mockUser.Email);
        Assert.Equal(SecurityConstants.Roles.Customer, mockUser.Role);
        Assert.True(mockUser.IsAuthenticated);
        Assert.False(mockUser.IsAdmin);
        Assert.True(mockUser.HasScope(SecurityConstants.Scopes.ReadWallet));
        Assert.False(mockUser.HasScope(SecurityConstants.Scopes.WriteWallet));
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(
            Guid? userId,
            string? email,
            string? role,
            IReadOnlyCollection<string> scopes,
            bool isAuthenticated)
        {
            UserId = userId;
            Email = email;
            Role = role;
            Scopes = scopes;
            IsAuthenticated = isAuthenticated;
        }

        public Guid? UserId { get; }
        public string? Email { get; }
        public string? Role { get; }
        public IReadOnlyCollection<string> Scopes { get; }
        public bool IsAuthenticated { get; }
        public bool IsAdmin => string.Equals(Role, SecurityConstants.Roles.Admin, StringComparison.OrdinalIgnoreCase);

        public bool HasScope(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}
