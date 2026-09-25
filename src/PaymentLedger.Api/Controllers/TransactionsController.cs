using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionBusinessService _transactionService;

    public TransactionsController(ITransactionBusinessService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundTransaction(
        Guid id,
        [FromBody] RefundTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken ct)
    {
        var transaction = await _transactionService.RefundTransactionAsync(
            id,
            request,
            idempotencyKeyHeader,
            ct);

        return Ok(ApiResponse<TransactionResponse>.Ok(
            transaction,
            "Transaction refunded successfully."));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransactionById(Guid id, CancellationToken ct)
    {
        var transaction = await _transactionService.GetTransactionDetailsAsync(id, ct);

        if (transaction == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "Transaction not found."
            });
        }

        return Ok(ApiResponse<TransactionDetailsResponse>.Ok(
            transaction,
            "Transaction details retrieved successfully."));
    }

    [HttpGet("{id}/verify")]
    public async Task<IActionResult> VerifyTransactionConsistency(Guid id, CancellationToken ct)
    {
        var audit = await _transactionService.VerifyTransactionConsistencyAsync(id, ct);

        return Ok(ApiResponse<TransactionAuditResponse>.Ok(
            audit,
            "Transaction consistency verification completed."));
    }
}
