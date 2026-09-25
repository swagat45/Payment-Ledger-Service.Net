using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Api.Controllers;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.UnitTests.Api;

public class ControllersTests
{
    [Fact]
    public async Task TransactionsController_RefundTransaction_ReturnsOkResult()
    {
        // Arrange
        var fakeTxService = new FakeTransactionBusinessService();
        var controller = new TransactionsController(fakeTxService);
        var txnId = Guid.NewGuid();
        var request = new RefundTransactionRequest(50m, "USD", "Test reason");

        // Act
        var result = await controller.RefundTransaction(txnId, request, "idem-key", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<TransactionResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal("Transaction refunded successfully.", apiResponse.Message);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(50m, apiResponse.Data.Amount);
    }

    [Fact]
    public async Task TransactionsController_GetTransactionById_WhenFound_ReturnsOkResult()
    {
        // Arrange
        var fakeTxService = new FakeTransactionBusinessService();
        var controller = new TransactionsController(fakeTxService);
        var txnId = Guid.NewGuid();
        fakeTxService.DetailsToReturn = new TransactionDetailsResponse(
            txnId,
            "TXN-DB-123",
            "Debit",
            "Posted",
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            "idem-key",
            null,
            "Description",
            null,
            100m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Array.Empty<LedgerEntryDto>(),
            0m,
            100m,
            Array.Empty<RefundSummaryDto>());

        // Act
        var result = await controller.GetTransactionById(txnId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<TransactionDetailsResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(txnId, apiResponse.Data.TransactionId);
    }

    [Fact]
    public async Task TransactionsController_GetTransactionById_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var fakeTxService = new FakeTransactionBusinessService();
        var controller = new TransactionsController(fakeTxService);
        fakeTxService.DetailsToReturn = null;

        // Act
        var result = await controller.GetTransactionById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Transaction not found.", errorResponse.Message);
    }

    [Fact]
    public async Task TransactionsController_VerifyTransactionConsistency_ReturnsOkResult()
    {
        // Arrange
        var fakeTxService = new FakeTransactionBusinessService();
        var controller = new TransactionsController(fakeTxService);
        var txnId = Guid.NewGuid();

        // Act
        var result = await controller.VerifyTransactionConsistency(txnId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<TransactionAuditResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data.IsBalanced);
    }

    [Fact]
    public async Task WalletsController_GetWalletTransactions_ReturnsOkResult()
    {
        // Arrange
        var fakeWalletService = new FakeWalletBusinessService();
        var controller = new WalletsController(fakeWalletService);
        var walletId = Guid.NewGuid();

        // Act
        var result = await controller.GetWalletTransactions(
            walletId,
            pageNumber: 1,
            pageSize: 10,
            type: null,
            status: null,
            fromDate: null,
            toDate: null,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResponse = Assert.IsType<PagedResponse<TransactionResponse>>(okResult.Value);
        Assert.True(pagedResponse.Success);
        Assert.Equal(1, pagedResponse.PageNumber);
        Assert.Equal(10, pagedResponse.PageSize);
    }

    [Fact]
    public async Task WalletsController_VerifyWalletConsistency_ReturnsOkResult()
    {
        // Arrange
        var fakeWalletService = new FakeWalletBusinessService();
        var controller = new WalletsController(fakeWalletService);
        var walletId = Guid.NewGuid();

        // Act
        var result = await controller.VerifyWalletConsistency(walletId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<WalletAuditResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data.IsConsistent);
    }

    [Fact]
    public async Task LedgerController_VerifyLedger_ReturnsOkResult()
    {
        // Arrange
        var fakeTxService = new FakeTransactionBusinessService();
        var controller = new LedgerController(fakeTxService);

        // Act
        var result = await controller.VerifyLedger(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<LedgerConsistencyResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data.IsConsistent);
    }

    #region Fake Business Services

    private sealed class FakeTransactionBusinessService : ITransactionBusinessService
    {
        public TransactionDetailsResponse? DetailsToReturn { get; set; }

        public Task<TransactionResponse> RefundTransactionAsync(Guid transactionId, RefundTransactionRequest request, string? idempotencyKeyHeader, CancellationToken ct = default)
        {
            return Task.FromResult(new TransactionResponse(
                TransactionId: Guid.NewGuid(),
                TransactionCode: "TXN-RF-001",
                Type: "Refund",
                Status: "Posted",
                SourceWalletId: Guid.NewGuid(),
                DestinationWalletId: Guid.NewGuid(),
                Amount: request.Amount,
                Currency: request.Currency,
                IdempotencyKey: idempotencyKeyHeader ?? "auto-key",
                Description: request.Reason,
                Metadata: request.Metadata,
                BalanceAfterTransaction: 100m,
                CreatedAt: DateTimeOffset.UtcNow));
        }

        public Task<TransactionDetailsResponse?> GetTransactionDetailsAsync(Guid transactionId, CancellationToken ct = default)
        {
            return Task.FromResult(DetailsToReturn);
        }

        public Task<PagedResponse<TransactionResponse>> GetWalletTransactionsPagedAsync(Guid walletId, GetWalletTransactionsRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(PagedResponse<TransactionResponse>.Ok(
                Array.Empty<TransactionResponse>(),
                request.PageNumber,
                request.PageSize,
                0,
                "Wallet transactions retrieved successfully."));
        }

        public Task<WalletAuditResponse> VerifyWalletConsistencyAsync(Guid walletId, CancellationToken ct = default)
        {
            return Task.FromResult(new WalletAuditResponse(
                walletId,
                Guid.NewGuid(),
                "USD",
                100m,
                100m,
                0m,
                true,
                DateTimeOffset.UtcNow));
        }

        public Task<TransactionAuditResponse> VerifyTransactionConsistencyAsync(Guid transactionId, CancellationToken ct = default)
        {
            return Task.FromResult(new TransactionAuditResponse(
                transactionId,
                "TXN-DB-1",
                50m,
                50m,
                50m,
                true,
                "Transaction double-entry ledger is perfectly balanced.",
                DateTimeOffset.UtcNow));
        }

        public Task<LedgerConsistencyResponse> VerifySystemLedgerConsistencyAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new LedgerConsistencyResponse(
                true,
                5,
                10,
                20,
                Array.Empty<TransactionDiscrepancyDto>(),
                Array.Empty<WalletDiscrepancyDto>(),
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeWalletBusinessService : IWalletBusinessService
    {
        public Task<WalletResponse> CreateWalletAsync(CreateWalletRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WalletResponse?> GetWalletByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WalletBalanceResponse?> GetWalletBalanceAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<WalletResponse>> GetWalletsByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WalletResponse?> BlockWalletAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WalletResponse?> UnblockWalletAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WalletResponse?> CloseWalletAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TransactionResponse> CreditWalletAsync(Guid walletId, CreditWalletRequest request, string? idempotencyKeyHeader, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TransactionResponse> DebitWalletAsync(Guid walletId, DebitWalletRequest request, string? idempotencyKeyHeader, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<PagedResponse<TransactionResponse>> GetWalletTransactionsPagedAsync(Guid walletId, GetWalletTransactionsRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(PagedResponse<TransactionResponse>.Ok(
                Array.Empty<TransactionResponse>(),
                request.PageNumber,
                request.PageSize,
                0,
                "Wallet transactions retrieved successfully."));
        }

        public Task<WalletAuditResponse> VerifyWalletConsistencyAsync(Guid walletId, CancellationToken ct = default)
        {
            return Task.FromResult(new WalletAuditResponse(
                walletId,
                Guid.NewGuid(),
                "USD",
                100m,
                100m,
                0m,
                true,
                DateTimeOffset.UtcNow));
        }
    }

    #endregion
}
