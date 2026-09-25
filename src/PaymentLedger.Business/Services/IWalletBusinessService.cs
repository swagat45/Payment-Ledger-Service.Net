using PaymentLedger.Business.Models;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Business.Services;

public interface IWalletBusinessService
{
    Task<WalletResponse> CreateWalletAsync(CreateWalletRequest request, CancellationToken ct = default);

    Task<WalletResponse?> GetWalletByIdAsync(Guid id, CancellationToken ct = default);

    Task<WalletBalanceResponse?> GetWalletBalanceAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyCollection<WalletResponse>> GetWalletsByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<WalletResponse?> BlockWalletAsync(Guid id, CancellationToken ct = default);

    Task<WalletResponse?> UnblockWalletAsync(Guid id, CancellationToken ct = default);

    Task<WalletResponse?> CloseWalletAsync(Guid id, CancellationToken ct = default);

    Task<TransactionResponse> CreditWalletAsync(
        Guid walletId,
        CreditWalletRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default);

    Task<TransactionResponse> DebitWalletAsync(
        Guid walletId,
        DebitWalletRequest request,
        string? idempotencyKeyHeader,
        CancellationToken ct = default);

    Task<PagedResponse<TransactionResponse>> GetWalletTransactionsPagedAsync(
        Guid walletId,
        GetWalletTransactionsRequest request,
        CancellationToken ct = default);

    Task<WalletAuditResponse> VerifyWalletConsistencyAsync(
        Guid walletId,
        CancellationToken ct = default);
}

// EVA-style naming alias
public interface IWalletBusiness : IWalletBusinessService
{
}
