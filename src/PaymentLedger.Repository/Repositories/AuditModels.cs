namespace PaymentLedger.Repository.Repositories;

public record TransactionDiscrepancy(
    Guid TransactionId,
    string TransactionCode,
    decimal TransactionAmount,
    decimal TotalDebits,
    decimal TotalCredits,
    string Reason);

public record WalletDiscrepancy(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal RecordedBalance,
    decimal CalculatedLedgerBalance,
    decimal Discrepancy);

public record SystemLedgerAuditResult(
    IReadOnlyCollection<TransactionDiscrepancy> TransactionDiscrepancies,
    IReadOnlyCollection<WalletDiscrepancy> WalletDiscrepancies,
    int TotalTransactionsChecked,
    int TotalWalletsChecked,
    int TotalLedgerEntriesChecked);
