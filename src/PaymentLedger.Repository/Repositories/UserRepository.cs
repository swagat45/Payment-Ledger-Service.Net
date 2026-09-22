using PaymentLedger.Repository.Persistence;
using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Users, long TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchText = null,
        PaymentLedger.Repository.Enums.UserStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(user => user.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim().ToLower();
            query = query.Where(user => user.FullName.ToLower().Contains(search) || user.Email.ToLower().Contains(search));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Update(User user)
    {
        _dbContext.Users.Update(user);
    }

    public void Delete(User user)
    {
        _dbContext.Users.Remove(user);
    }
}
