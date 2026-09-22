using PaymentLedger.Business.Models;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Services;

public interface IUserBusinessService
{
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResponse<UserResponse>> GetUsersPagedAsync(GetUsersRequest request, CancellationToken ct = default);

    Task<UserResponse?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default);
}

// EVA-style naming alias
public interface IUserBusiness : IUserBusinessService
{
}
