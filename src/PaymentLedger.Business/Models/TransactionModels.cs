namespace PaymentLedger.Business.Models;

// Requests
public record RefundTransactionRequest(
    decimal Amount,
    string Currency,
    string? Reason = null,
    string? IdempotencyKey = null,
    string? Metadata = null);

public record GetWalletTransactionsRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Type = null,
    string? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null);

public record VerifyLedgerRequest(
    Guid? WalletId = null,
    Guid? TransactionId = null,
    bool Detailed = false);

// DTOs & Responses
public record RefundSummaryDto(
    Guid TransactionId,
    string TransactionCode,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt);

public record TransactionDetailsResponse(
    Guid TransactionId,
    string TransactionCode,
    string Type,
    string Status,
    Guid? SourceWalletId,
    Guid? DestinationWalletId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    Guid? ReferenceTransactionId,
    string? Description,
    string? Metadata,
    decimal? BalanceAfterTransaction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<LedgerEntryDto> LedgerEntries,
    decimal TotalRefundedAmount,
    decimal RemainingRefundableAmount,
    IReadOnlyCollection<RefundSummaryDto> Refunds);

public record TransactionAuditResponse(
    Guid TransactionId,
    string TransactionCode,
    decimal TransactionAmount,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced,
    string Message,
    DateTimeOffset AuditedAt);

public record WalletAuditResponse(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal CurrentBalance,
    decimal CalculatedLedgerBalance,
    decimal Discrepancy,
    bool IsConsistent,
    DateTimeOffset AuditedAt);

public record TransactionDiscrepancyDto(
    Guid TransactionId,
    string TransactionCode,
    decimal TransactionAmount,
    decimal TotalDebits,
    decimal TotalCredits,
    string Reason);

public record WalletDiscrepancyDto(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal RecordedBalance,
    decimal CalculatedLedgerBalance,
    decimal Discrepancy);

public record LedgerConsistencyResponse(
    bool IsConsistent,
    int TotalWalletsAudited,
    int TotalTransactionsAudited,
    int TotalLedgerEntriesAudited,
    IReadOnlyCollection<TransactionDiscrepancyDto> TransactionDiscrepancies,
    IReadOnlyCollection<WalletDiscrepancyDto> WalletDiscrepancies,
    DateTimeOffset AuditedAt);
