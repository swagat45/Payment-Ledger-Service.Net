using MediatR;

namespace PaymentLedger.Application.Users.Queries.GetUser;

public record GetUserQuery(Guid UserId) : IRequest<UserDto?>;
