using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentLedger.Business.Models;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.Business.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenService(
        IOptions<JwtOptions> jwtOptions,
        IDateTimeProvider dateTimeProvider)
    {
        _jwtOptions = jwtOptions.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public TokenResponse GenerateToken(Guid userId, string email, string role, IReadOnlyCollection<string> scopes)
    {
        var key = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

        var now = _dateTimeProvider.UtcNow.UtcDateTime;
        var expires = now.AddMinutes(_jwtOptions.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new Claim(SecurityConstants.ClaimTypes.UserId, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(SecurityConstants.ClaimTypes.Email, email),
            new Claim(SecurityConstants.ClaimTypes.Role, role)
        };

        foreach (var scope in scopes)
        {
            claims.Add(new Claim(SecurityConstants.ClaimTypes.Scope, scope));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            NotBefore = now,
            IssuedAt = now,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        return new TokenResponse(
            AccessToken: accessToken,
            ExpiresInSeconds: _jwtOptions.ExpiryMinutes * 60,
            UserId: userId,
            Role: role,
            Scopes: scopes);
    }
}
