using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Users, long TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchText = null,
        PaymentLedger.Repository.Enums.UserStatus? status = null,
        CancellationToken cancellationToken = default);
    void Update(User user);
    void Delete(User user);
}
