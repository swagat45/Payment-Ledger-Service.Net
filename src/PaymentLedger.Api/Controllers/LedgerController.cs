using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/ledger")]
public class LedgerController : ControllerBase
{
    private readonly ITransactionBusinessService _transactionService;

    public LedgerController(ITransactionBusinessService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpPost("verify")]
    [HttpGet("verify")]
    public async Task<IActionResult> VerifyLedger(CancellationToken ct)
    {
        var audit = await _transactionService.VerifySystemLedgerConsistencyAsync(ct);

        return Ok(ApiResponse<LedgerConsistencyResponse>.Ok(
            audit,
            "Ledger consistency verification completed."));
    }
}
