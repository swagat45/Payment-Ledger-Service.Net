using MediatR;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Users.Queries.GetUser;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Application.Users.Queries.GetUsers;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResponse<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResponse<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        UserStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<UserStatus>(request.Status, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var (users, totalCount) = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchText,
            status,
            cancellationToken);

        var dtos = users.Select(user => new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.CreatedAt)).ToList();

        return PagedResponse<UserDto>.Ok(
            dtos,
            request.PageNumber,
            request.PageSize,
            totalCount,
            "Users retrieved successfully.");
    }
}
