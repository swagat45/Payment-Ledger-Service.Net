using MediatR;

namespace PaymentLedger.Application.Users.Commands.CreateUser;

public record CreateUserCommand(string FullName, string Email) : IRequest<Guid>;
