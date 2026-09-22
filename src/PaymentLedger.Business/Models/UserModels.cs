namespace PaymentLedger.Business.Models;

// Requests
public record CreateUserRequest(string FullName, string Email);

public record UpdateUserRequest(string FullName, string Email);

public record GetUsersRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchText = null,
    string? Status = null);

// Responses / View Models / DTOs
public record UserResponse(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Guid UserId => Id;
}

// Convenient alias
public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : UserResponse(Id, FullName, Email, Status, CreatedAt, UpdatedAt);
