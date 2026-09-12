using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Users.Commands.CreateUser;
using PaymentLedger.Application.Users.Commands.DeleteUser;
using PaymentLedger.Application.Users.Commands.UpdateUser;
using PaymentLedger.Application.Users.Queries.GetUser;
using PaymentLedger.Application.Users.Queries.GetUsers;
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

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        [FromQuery] string? status = null)
    {
        var query = new GetUsersQuery(pageNumber, pageSize, searchText, status);
        var response = await _mediator.Send(query);
        return Ok(response);
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Email);
        var user = await _mediator.Send(command);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(ApiResponse<UserDto>.Ok(user, "User updated successfully."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var command = new DeleteUserCommand(id);
        var success = await _mediator.Send(command);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(ApiResponse<object>.Ok(new { UserId = id }, "User deleted successfully."));
    }
}

public record UpdateUserRequest(string FullName, string Email);
