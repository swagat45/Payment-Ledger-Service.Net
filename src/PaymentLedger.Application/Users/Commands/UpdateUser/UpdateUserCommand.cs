using MediatR;
using PaymentLedger.Application.Users.Queries.GetUser;

namespace PaymentLedger.Application.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Email) : IRequest<UserDto?>;
