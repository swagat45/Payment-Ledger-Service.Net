using System.ComponentModel.DataAnnotations;
using PaymentLedger.Business.Models;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Responses;
using ValidationException = PaymentLedger.Business.Exceptions.ValidationException;

namespace PaymentLedger.Business.Services;

public class UserBusinessService : IUserBusiness, IUserBusinessService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly IUserRepository _userRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserBusinessService(
        IUserRepository userRepository,
        IWalletRepository walletRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        ValidateCreateUserRequest(request);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.EmailExistsAsync(email, ct))
        {
            throw new DomainException("A user with this email already exists.", ErrorCodes.UserAlreadyExists);
        }

        var user = User.Create(
            request.FullName.Trim(),
            email,
            _dateTimeProvider.UtcNow);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(user);
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        var user = await _userRepository.GetByIdAsync(id, ct);
        return user == null ? null : MapToResponse(user);
    }

    public async Task<PagedResponse<UserResponse>> GetUsersPagedAsync(GetUsersRequest request, CancellationToken ct = default)
    {
        if (request.PageNumber < 1)
        {
            throw new ValidationException("PageNumber", "Page number must be at least 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ValidationException("PageSize", "Page size must be between 1 and 100.");
        }

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
            ct);

        var responses = users.Select(MapToResponse).ToList();

        return PagedResponse<UserResponse>.Ok(
            responses,
            request.PageNumber,
            request.PageSize,
            totalCount,
            "Users retrieved successfully.");
    }

    public async Task<UserResponse?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        ValidateUpdateUserRequest(request);

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null)
        {
            return null;
        }

        var newEmail = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _userRepository.EmailExistsAsync(newEmail, ct);
            if (emailExists)
            {
                throw new DomainException("A user with this email already exists.", ErrorCodes.UserAlreadyExists);
            }
        }

        user.Update(request.FullName.Trim(), newEmail, _dateTimeProvider.UtcNow);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(user);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null)
        {
            return false;
        }

        var wallets = await _walletRepository.ListByUserIdAsync(id, ct);
        if (wallets.Count > 0)
        {
            throw new DomainException("Cannot delete user with associated wallets.", ErrorCodes.UserHasAssociatedWallets);
        }

        _userRepository.Delete(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return true;
    }

    private static void ValidateCreateUserRequest(CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["FullName"] = ["Full name is required."];
        }
        else if (request.FullName.Length > 200)
        {
            errors["FullName"] = ["Full name must not exceed 200 characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["Email"] = ["Email is required."];
        }
        else if (request.Email.Length > 320)
        {
            errors["Email"] = ["Email must not exceed 320 characters."];
        }
        else if (!EmailValidator.IsValid(request.Email))
        {
            errors["Email"] = ["Email format is invalid."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void ValidateUpdateUserRequest(UpdateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["FullName"] = ["Full name is required."];
        }
        else if (request.FullName.Length > 200)
        {
            errors["FullName"] = ["Full name must not exceed 200 characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["Email"] = ["Email is required."];
        }
        else if (request.Email.Length > 320)
        {
            errors["Email"] = ["Email must not exceed 320 characters."];
        }
        else if (!EmailValidator.IsValid(request.Email))
        {
            errors["Email"] = ["A valid email address is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static UserResponse MapToResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.CreatedAt,
            user.UpdatedAt);
    }
}
