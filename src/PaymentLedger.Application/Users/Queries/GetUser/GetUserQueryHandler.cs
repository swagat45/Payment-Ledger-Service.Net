using MediatR;
using PaymentLedger.Application.Abstractions.Persistence;

namespace PaymentLedger.Application.Users.Queries.GetUser;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        return new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.CreatedAt);
    }
}
