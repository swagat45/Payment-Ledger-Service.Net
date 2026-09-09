namespace PaymentLedger.Application.Users.Queries.GetUser;

public record UserDto(
    Guid UserId,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset CreatedAt);
