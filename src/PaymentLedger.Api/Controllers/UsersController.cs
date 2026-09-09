using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Users.Commands.CreateUser;
using PaymentLedger.Application.Users.Queries.GetUser;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        var userId = await _mediator.Send(command);

        var response = ApiResponse<object>.Ok(
            new { UserId = userId, command.FullName, command.Email, Status = "Active" }, 
            "User created successfully.");

        return CreatedAtAction(nameof(GetUserById), new { id = userId }, response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var query = new GetUserQuery(id);
        var user = await _mediator.Send(query);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(ApiResponse<UserDto>.Ok(user, "User retrieved successfully."));
    }
}
