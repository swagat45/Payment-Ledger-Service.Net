using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        // For development/B2B simulation: We issue a token based on the request payload directly.
        // In a real system, you would validate credentials here before issuing.
        
        var response = _jwtTokenService.GenerateToken(request.UserId, request.Email, request.Role, request.Scopes);
        
        return Ok(ApiResponse<TokenResponse>.Ok(response, "Token generated successfully."));
    }
}

