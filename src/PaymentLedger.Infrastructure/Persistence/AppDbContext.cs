using Microsoft.EntityFrameworkCore;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence;

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
}
