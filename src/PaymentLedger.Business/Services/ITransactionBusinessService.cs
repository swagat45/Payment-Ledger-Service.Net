using PaymentLedger.Business.Models;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Services;

public interface ITransactionBusinessService
{
    Task<TransactionResponse> RefundTransactionAsync(
        Guid transactionId,
        RefundTransactionRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default);

    Task<TransactionDetailsResponse?> GetTransactionDetailsAsync(
        Guid transactionId,
        CancellationToken ct = default);

    Task<PagedResponse<TransactionResponse>> GetWalletTransactionsPagedAsync(
        Guid walletId,
        GetWalletTransactionsRequest request,
        CancellationToken ct = default);

    Task<WalletAuditResponse> VerifyWalletConsistencyAsync(
        Guid walletId,
        CancellationToken ct = default);

    Task<TransactionAuditResponse> VerifyTransactionConsistencyAsync(
        Guid transactionId,
        CancellationToken ct = default);

    Task<LedgerConsistencyResponse> VerifySystemLedgerConsistencyAsync(
        CancellationToken ct = default);
}

public interface ITransactionBusiness : ITransactionBusinessService
{
}
