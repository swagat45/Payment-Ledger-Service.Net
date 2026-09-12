using MediatR;
using Microsoft.AspNetCore.Mvc;
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

    public WalletsController(IMediator mediator)
    {
        _mediator = mediator;
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
}
