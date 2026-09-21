using PaymentLedger.Business.Exceptions;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Business;

public class UserBusinessServiceTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);

    private UserBusinessService CreateService() =>
        new(_userRepository, _walletRepository, _unitOfWork, _dateTimeProvider);

    [Fact]
    public async Task CreateUserAsync_WithValidData_CreatesAndReturnsUser()
    {
        var service = CreateService();
        var request = new CreateUserRequest("Alice Smith", "alice@example.com");

        var response = await service.CreateUserAsync(request);

        Assert.NotEqual(Guid.Empty, response.UserId);
        Assert.Equal("Alice Smith", response.FullName);
        Assert.Equal("alice@example.com", response.Email);
        Assert.Equal("Active", response.Status);
        Assert.True(_unitOfWork.ChangesSaved);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailExists_ThrowsDomainException()
    {
        var service = CreateService();
        var existing = User.Create("Bob", "existing@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(existing);

        var request = new CreateUserRequest("Bob New", "existing@example.com");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateUserAsync(request));
        Assert.Equal(ErrorCodes.UserAlreadyExists, ex.ErrorCode);
    }

    [Theory]
    [InlineData("", "valid@example.com")]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "invalid-email")]
    public async Task CreateUserAsync_WithInvalidData_ThrowsValidationException(string name, string email)
    {
        var service = CreateService();
        var request = new CreateUserRequest(name, email);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateUserAsync(request));
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenExists_ReturnsUser()
    {
        var service = CreateService();
        var user = User.Create("Charlie", "charlie@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var result = await service.GetUserByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Charlie", result.FullName);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenNotFound_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetUserByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUsersPagedAsync_ReturnsPagedResults()
    {
        var service = CreateService();
        for (int i = 1; i <= 5; i++)
        {
            await _userRepository.AddAsync(User.Create($"User {i}", $"user{i}@example.com", _dateTimeProvider.UtcNow));
        }

        var result = await service.GetUsersPagedAsync(new GetUsersRequest(1, 10));

        Assert.Equal(5, result.Data.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidData_UpdatesUser()
    {
        var service = CreateService();
        var user = User.Create("Old Name", "old@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var request = new UpdateUserRequest("New Name", "new@example.com");
        var result = await service.UpdateUserAsync(user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("New Name", result.FullName);
        Assert.Equal("new@example.com", result.Email);
        Assert.True(_unitOfWork.ChangesSaved);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserHasWallets_ThrowsDomainException()
    {
        var service = CreateService();
        var user = User.Create("User With Wallet", "haswallet@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var wallet = Wallet.Create(user.Id, "USD", _dateTimeProvider.UtcNow);
        await _walletRepository.AddAsync(wallet);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteUserAsync(user.Id));
        Assert.Equal(ErrorCodes.UserHasAssociatedWallets, ex.ErrorCode);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenNoWallets_DeletesUser()
    {
        var service = CreateService();
        var user = User.Create("Delete Me", "delete@example.com", _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(user);

        var result = await service.DeleteUserAsync(user.Id);

        Assert.True(result);
        Assert.True(_unitOfWork.ChangesSaved);
    }

    #region Fakes

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = new();

        public Task AddAsync(User user, CancellationToken ct = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<User>>(_users);

        public Task<(IReadOnlyList<User> Users, long TotalCount)> GetPagedAsync(
            int pageNumber, int pageSize, string? searchText = null, UserStatus? status = null, CancellationToken ct = default)
        {
            var query = _users.AsQueryable();
            if (status.HasValue) query = query.Where(u => u.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(searchText)) query = query.Where(u => u.FullName.Contains(searchText) || u.Email.Contains(searchText));
            var list = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(((IReadOnlyList<User>)list, (long)_users.Count));
        }

        public void Update(User user) { }

        public void Delete(User user) => _users.Remove(user);
    }

    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly List<Wallet> _wallets = new();

        public Task AddAsync(Wallet wallet, CancellationToken ct = default)
        {
            _wallets.Add(wallet);
            return Task.CompletedTask;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_wallets.FirstOrDefault(w => w.Id == id));

        public Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_wallets.FirstOrDefault(w => w.Id == id));

        public Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken ct = default) =>
            Task.FromResult(_wallets.FirstOrDefault(w => w.UserId == userId && w.Currency == currency));

        public Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Wallet>>(_wallets.Where(w => w.UserId == userId).ToList());

        public Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken ct = default) =>
            Task.FromResult(_wallets.FirstOrDefault(w => w.Currency == currency));

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_wallets.Any(w => w.Id == id));

        public void Update(Wallet wallet) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool ChangesSaved { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ChangesSaved = true;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; }
        public FakeDateTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;
    }

    #endregion
}
