using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Wallets.Commands.BlockWallet;
using PaymentLedger.Application.Wallets.Commands.CloseWallet;
using PaymentLedger.Application.Wallets.Commands.CreateWallet;
using PaymentLedger.Application.Wallets.Commands.UnblockWallet;
using PaymentLedger.Application.Wallets.Common;
using PaymentLedger.Application.Wallets.Queries.GetWalletBalance;
using PaymentLedger.Application.Wallets.Queries.GetWalletById;
using PaymentLedger.Application.Wallets.Queries.GetWalletsByUserId;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
public class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IIdempotencyService _idempotencyService;

    public WalletsController(IMediator mediator, IIdempotencyService idempotencyService)
    {
        _mediator = mediator;
        _idempotencyService = idempotencyService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletCommand command)
    {
        var wallet = await _mediator.Send(command);

        var response = ApiResponse<WalletDto>.Ok(wallet, "Wallet created successfully.");

        return CreatedAtAction(nameof(GetWalletById), new { id = wallet.WalletId }, response);
    }

    [HttpGet("{id}/balance")]
    public async Task<IActionResult> GetWalletBalance(Guid id)
    {
        var query = new GetWalletBalanceQuery(id);
        var balance = await _mediator.Send(query);

        if (balance == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletBalanceDto>.Ok(balance, "Balance retrieved successfully."));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWalletById(Guid id)
    {
        var query = new GetWalletByIdQuery(id);
        var wallet = await _mediator.Send(query);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletDto>.Ok(wallet, "Wallet retrieved successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetWalletsByUserId([FromQuery] Guid userId)
    {
        var query = new GetWalletsByUserIdQuery(userId);
        var wallets = await _mediator.Send(query);

        return Ok(ApiResponse<IReadOnlyCollection<WalletDto>>.Ok(wallets, "Wallets retrieved successfully."));
    }

    [HttpPatch("{id}/block")]
    public async Task<IActionResult> BlockWallet(Guid id)
    {
        var command = new BlockWalletCommand(id);
        var wallet = await _mediator.Send(command);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletDto>.Ok(wallet, "Wallet blocked successfully."));
    }

    [HttpPatch("{id}/unblock")]
    public async Task<IActionResult> UnblockWallet(Guid id)
    {
        var command = new UnblockWalletCommand(id);
        var wallet = await _mediator.Send(command);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletDto>.Ok(wallet, "Wallet unblocked successfully."));
    }

    [HttpPatch("{id}/close")]
    public async Task<IActionResult> CloseWallet(Guid id)
    {
        var command = new CloseWalletCommand(id);
        var wallet = await _mediator.Send(command);

        if (wallet == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Wallet not found."
            });
        }

        return Ok(ApiResponse<WalletDto>.Ok(wallet, "Wallet closed successfully."));
    }

    [HttpPost("{id}/credit")]
    public async Task<IActionResult> CreditWallet(
        Guid id,
        [FromBody] CreditWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader)
    {
        var idempotencyKey = _idempotencyService.ResolveKey(
            !string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? idempotencyKeyHeader : request.IdempotencyKey,
            "credit",
            id,
            request.ReferenceId,
            request.Amount);

        var command = new PaymentLedger.Application.Wallets.Commands.CreditWallet.CreditWalletCommand(
            id,
            request.Amount,
            request.Currency,
            idempotencyKey,
            request.Description,
            request.Metadata,
            request.ReferenceId);

        var transaction = await _mediator.Send(command);

        return Ok(ApiResponse<PaymentLedger.Application.Transactions.Common.TransactionDto>.Ok(
            transaction,
            "Wallet credited successfully."));
    }

    [HttpPost("{id}/debit")]
    public async Task<IActionResult> DebitWallet(
        Guid id,
        [FromBody] DebitWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader)
    {
        var idempotencyKey = _idempotencyService.ResolveKey(
            !string.IsNullOrWhiteSpace(idempotencyKeyHeader) ? idempotencyKeyHeader : request.IdempotencyKey,
            "debit",
            id,
            request.ReferenceId,
            request.Amount);

        var command = new PaymentLedger.Application.Wallets.Commands.DebitWallet.DebitWalletCommand(
            id,
            request.Amount,
            request.Currency,
            idempotencyKey,
            request.Description,
            request.Metadata,
            request.ReferenceId);

        var transaction = await _mediator.Send(command);

        return Ok(ApiResponse<PaymentLedger.Application.Transactions.Common.TransactionDto>.Ok(
            transaction,
            "Wallet debited successfully."));
    }
}

public record CreditWalletRequest(
    decimal Amount,
    string Currency,
    string? IdempotencyKey = null,
    string? ReferenceId = null,
    string? Description = null,
    string? Metadata = null);

public record DebitWalletRequest(
    decimal Amount,
    string Currency,
    string? IdempotencyKey = null,
    string? ReferenceId = null,
    string? Description = null,
    string? Metadata = null);
