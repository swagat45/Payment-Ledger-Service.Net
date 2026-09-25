using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Persistence;

namespace PaymentLedger.Repository.Repositories;

public sealed class LedgerRepository : ILedgerRepository
{
    private readonly AppDbContext _dbContext;

    public LedgerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
    {
        return _dbContext.LedgerEntries.AddRangeAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetWalletLedgerSumAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var credits = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var debits = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Debit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        return credits - debits;
    }

    public async Task<(decimal TotalDebits, decimal TotalCredits)> GetTransactionLedgerSumAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var debits = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionId == transactionId && e.EntryType == LedgerEntryType.Debit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var credits = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionId == transactionId && e.EntryType == LedgerEntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        return (debits, credits);
    }

    public async Task<SystemLedgerAuditResult> GetSystemLedgerDiscrepanciesAsync(
        CancellationToken cancellationToken = default)
    {
        var totalEntries = await _dbContext.LedgerEntries.CountAsync(cancellationToken);
        var transactions = await _dbContext.Transactions.AsNoTracking().ToListAsync(cancellationToken);
        var wallets = await _dbContext.Wallets.AsNoTracking().ToListAsync(cancellationToken);

        var transactionGroups = await _dbContext.LedgerEntries
            .AsNoTracking()
            .GroupBy(e => e.TransactionId)
            .Select(g => new
            {
                TransactionId = g.Key,
                TotalDebits = g.Sum(e => e.EntryType == LedgerEntryType.Debit ? e.Amount : 0m),
                TotalCredits = g.Sum(e => e.EntryType == LedgerEntryType.Credit ? e.Amount : 0m)
            })
            .ToListAsync(cancellationToken);

        var txnGroupDict = transactionGroups.ToDictionary(g => g.TransactionId);
        var transactionDiscrepancies = new List<TransactionDiscrepancy>();

        foreach (var txn in transactions)
        {
            if (!txnGroupDict.TryGetValue(txn.Id, out var group))
            {
                transactionDiscrepancies.Add(new TransactionDiscrepancy(
                    txn.Id,
                    txn.TransactionCode,
                    txn.Amount,
                    0m,
                    0m,
                    "No ledger entries found for transaction."));
                continue;
            }

            if (group.TotalDebits != group.TotalCredits)
            {
                transactionDiscrepancies.Add(new TransactionDiscrepancy(
                    txn.Id,
                    txn.TransactionCode,
                    txn.Amount,
                    group.TotalDebits,
                    group.TotalCredits,
                    $"Double-entry unbalanced: Debits ({group.TotalDebits}) != Credits ({group.TotalCredits})."));
            }
            else if (group.TotalDebits != txn.Amount)
            {
                transactionDiscrepancies.Add(new TransactionDiscrepancy(
                    txn.Id,
                    txn.TransactionCode,
                    txn.Amount,
                    group.TotalDebits,
                    group.TotalCredits,
                    $"Entry amount mismatch: Ledger entries sum ({group.TotalDebits}) != Transaction amount ({txn.Amount})."));
            }
        }

        var walletGroups = await _dbContext.LedgerEntries
            .AsNoTracking()
            .GroupBy(e => e.WalletId)
            .Select(g => new
            {
                WalletId = g.Key,
                NetBalance = g.Sum(e => e.EntryType == LedgerEntryType.Credit ? e.Amount : -e.Amount)
            })
            .ToListAsync(cancellationToken);

        var walletGroupDict = walletGroups.ToDictionary(g => g.WalletId, g => g.NetBalance);
        var walletDiscrepancies = new List<WalletDiscrepancy>();

        foreach (var wallet in wallets)
        {
            var calculatedBalance = walletGroupDict.TryGetValue(wallet.Id, out var net) ? net : 0m;
            if (wallet.Balance != calculatedBalance)
            {
                walletDiscrepancies.Add(new WalletDiscrepancy(
                    wallet.Id,
                    wallet.UserId,
                    wallet.Currency,
                    wallet.Balance,
                    calculatedBalance,
                    wallet.Balance - calculatedBalance));
            }
        }

        return new SystemLedgerAuditResult(
            transactionDiscrepancies,
            walletDiscrepancies,
            transactions.Count,
            wallets.Count,
            totalEntries);
    }
}
