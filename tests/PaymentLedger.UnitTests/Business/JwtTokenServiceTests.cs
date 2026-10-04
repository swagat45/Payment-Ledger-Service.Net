using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.UnitTests.Business;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _options;
    private readonly JwtTokenService _sut;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    public JwtTokenServiceTests()
    {
        _options = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "SuperSecret_Test_Key_12345678901234567890", // > 32 bytes
            ExpiryMinutes = 60
        };

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
        var mockTimeProvider = new TestDateTimeProvider(_fixedTime);

        _sut = new JwtTokenService(optionsWrapper, mockTimeProvider);
    }

    [Fact]
    public void GenerateToken_CreatesValidTokenWithExpectedClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "alice@example.com";
        var role = SecurityConstants.Roles.Customer;
        var scopes = new[] { SecurityConstants.Scopes.ReadWallet, SecurityConstants.Scopes.WriteWallet };

        // Act
        var response = _sut.GenerateToken(userId, email, role, scopes);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.AccessToken);
        Assert.Equal(3600, response.ExpiresInSeconds);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(role, response.Role);
        Assert.Equal(scopes, response.Scopes);
        Assert.Equal("Bearer", response.TokenType);

        // Validate raw token claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(response.AccessToken);

        Assert.Equal("TestIssuer", jwtToken.Issuer);
        Assert.Contains(jwtToken.Audiences, a => a == "TestAudience");
        
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == SecurityConstants.ClaimTypes.UserId);
        Assert.NotNull(subClaim);
        Assert.Equal(userId.ToString(), subClaim.Value);

        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == SecurityConstants.ClaimTypes.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal(email, emailClaim.Value);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == SecurityConstants.ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(role, roleClaim.Value);

        var scopeClaims = jwtToken.Claims.Where(c => c.Type == SecurityConstants.ClaimTypes.Scope).Select(c => c.Value).ToList();
        Assert.Equal(2, scopeClaims.Count);
        Assert.Contains(SecurityConstants.Scopes.ReadWallet, scopeClaims);
        Assert.Contains(SecurityConstants.Scopes.WriteWallet, scopeClaims);

        // Verify expiration
        Assert.Equal(_fixedTime.UtcDateTime.AddMinutes(60), jwtToken.ValidTo);
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; }
        public TestDateTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;
    }
}
