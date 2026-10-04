namespace PaymentLedger.Business.Models;

public record TokenRequest(
    Guid UserId,
    string Email,
    string Role,
    IReadOnlyCollection<string> Scopes);

public record LoginRequest(
    string Email,
    string Password);

public record TokenResponse(
    string AccessToken,
    int ExpiresInSeconds,
    Guid UserId,
    string Role,
    IReadOnlyCollection<string> Scopes,
    string TokenType = "Bearer");
