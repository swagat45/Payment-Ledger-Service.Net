using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Persistence;

public sealed class AppDbContext : DbContext, IAppDbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<Wallet> Wallets { get; set; } = null!;

    public DbSet<Transaction> Transactions { get; set; } = null!;

    public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;

    public DbSet<IdempotencyRequest> IdempotencyRequests { get; set; } = null!;

    IQueryable<User> IAppDbContext.Users => Users;

    IQueryable<Wallet> IAppDbContext.Wallets => Wallets;

    IQueryable<Transaction> IAppDbContext.Transactions => Transactions;

    IQueryable<LedgerEntry> IAppDbContext.LedgerEntries => LedgerEntries;

    IQueryable<IdempotencyRequest> IAppDbContext.IdempotencyRequests => IdempotencyRequests;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction != null)
        {
            return Task.CompletedTask;
        }

        return Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction != null)
        {
            await using (currentTransaction)
            {
                await currentTransaction.CommitAsync(cancellationToken);
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction != null)
        {
            await using (currentTransaction)
            {
                await currentTransaction.RollbackAsync(cancellationToken);
            }
        }
    }
}
