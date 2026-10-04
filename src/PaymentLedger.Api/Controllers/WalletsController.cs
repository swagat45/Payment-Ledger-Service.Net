using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;
using PaymentLedger.Shared.Constants;
using Microsoft.AspNetCore.Authorization;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Authorize(Policy = SecurityConstants.Policies.ReadWallet)]
public class WalletsController : ControllerBase
{
    private readonly IWalletBusinessService _walletService;

    public WalletsController(IWalletBusinessService walletService)
    {
        _walletService = walletService;
    }

    [HttpPost]
    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequest request, CancellationToken ct)
    {
        var wallet = await _walletService.CreateWalletAsync(request, ct);

        var response = ApiResponse<WalletResponse>.Ok(wallet, "Wallet created successfully.");

        return CreatedAtAction(nameof(GetWalletById), new { id = wallet.WalletId }, response);
    }

    [HttpGet("{id}/balance")]
    public async Task<IActionResult> GetWalletBalance(Guid id, CancellationToken ct)
    {
        var balance = await _walletService.GetWalletBalanceAsync(id, ct);

        if (balance == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletBalanceResponse>.Ok(balance, "Balance retrieved successfully."));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWalletById(Guid id, CancellationToken ct)
    {
        var wallet = await _walletService.GetWalletByIdAsync(id, ct);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletResponse>.Ok(wallet, "Wallet retrieved successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetWalletsByUserId([FromQuery] Guid userId, CancellationToken ct)
    {
        var wallets = await _walletService.GetWalletsByUserIdAsync(userId, ct);

        return Ok(ApiResponse<IReadOnlyCollection<WalletResponse>>.Ok(wallets, "Wallets retrieved successfully."));
    }

    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    [HttpPatch("{id}/block")]
    public async Task<IActionResult> BlockWallet(Guid id, CancellationToken ct)
    {
        var wallet = await _walletService.BlockWalletAsync(id, ct);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletResponse>.Ok(wallet, "Wallet blocked successfully."));
    }

    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    [HttpPatch("{id}/unblock")]
    public async Task<IActionResult> UnblockWallet(Guid id, CancellationToken ct)
    {
        var wallet = await _walletService.UnblockWalletAsync(id, ct);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletResponse>.Ok(wallet, "Wallet unblocked successfully."));
    }

    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    [HttpPatch("{id}/close")]
    public async Task<IActionResult> CloseWallet(Guid id, CancellationToken ct)
    {
        var wallet = await _walletService.CloseWalletAsync(id, ct);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletResponse>.Ok(wallet, "Wallet closed successfully."));
    }

    [HttpPost("{id}/credit")]
    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    public async Task<IActionResult> CreditWallet(
        Guid id,
        [FromBody] CreditWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken ct)
    {
        var transaction = await _walletService.CreditWalletAsync(
            id,
            request,
            idempotencyKeyHeader,
            ct);

        return Ok(ApiResponse<TransactionResponse>.Ok(
            transaction,
            "Wallet credited successfully."));
    }

    [HttpPost("{id}/debit")]
    [Authorize(Policy = SecurityConstants.Policies.WriteWallet)]
    public async Task<IActionResult> DebitWallet(
        Guid id,
        [FromBody] DebitWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken ct)
    {
        var transaction = await _walletService.DebitWalletAsync(
            id,
            request,
            idempotencyKeyHeader,
            ct);

        return Ok(ApiResponse<TransactionResponse>.Ok(
            transaction,
            "Wallet debited successfully."));
    }

    [HttpGet("{id}/transactions")]
    public async Task<IActionResult> GetWalletTransactions(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var request = new GetWalletTransactionsRequest(pageNumber, pageSize, type, status, fromDate, toDate);
        var pagedResponse = await _walletService.GetWalletTransactionsPagedAsync(id, request, ct);

        return Ok(pagedResponse);
    }

    [HttpGet("{id}/verify")]
    public async Task<IActionResult> VerifyWalletConsistency(Guid id, CancellationToken ct)
    {
        var audit = await _walletService.VerifyWalletConsistencyAsync(id, ct);

        return Ok(ApiResponse<WalletAuditResponse>.Ok(
            audit,
            "Wallet consistency verification completed."));
    }
}


