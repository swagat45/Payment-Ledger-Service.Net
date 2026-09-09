using MediatR;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Exceptions;

namespace PaymentLedger.Application.Users.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new DomainException("A user with this email already exists.");
        }

        var user = User.Create(
            request.FullName, 
            request.Email, 
            _dateTimeProvider.UtcNow);

        await _userRepository.AddAsync(user, cancellationToken);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
