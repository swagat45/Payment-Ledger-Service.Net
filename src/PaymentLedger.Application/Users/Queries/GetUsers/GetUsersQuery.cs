using MediatR;
using PaymentLedger.Application.Users.Queries.GetUser;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Application.Users.Queries.GetUsers;

public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchText = null,
    string? Status = null) : IRequest<PagedResponse<UserDto>>;
