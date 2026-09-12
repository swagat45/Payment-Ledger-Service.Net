using MediatR;

namespace PaymentLedger.Application.Users.Commands.DeleteUser;

public record DeleteUserCommand(Guid UserId) : IRequest<bool>;
