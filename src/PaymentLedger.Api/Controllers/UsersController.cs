using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Business.Models;
using PaymentLedger.Business.Services;
using PaymentLedger.Shared.Responses;
using PaymentLedger.Shared.Constants;
using Microsoft.AspNetCore.Authorization;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = SecurityConstants.Policies.AdminLedger)]
public class UsersController : ControllerBase
{
    private readonly IUserBusinessService _userService;

    public UsersController(IUserBusinessService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.CreateUserAsync(request, ct);

        var response = ApiResponse<UserResponse>.Ok(user, "User created successfully.");

        return CreatedAtAction(nameof(GetUserById), new { id = user.UserId }, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var query = new GetUsersRequest(pageNumber, pageSize, searchText, status);
        var response = await _userService.GetUsersPagedAsync(query, ct);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(ApiResponse<UserResponse>.Ok(user, "User retrieved successfully."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.UpdateUserAsync(id, request, ct);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(ApiResponse<UserResponse>.Ok(user, "User updated successfully."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var success = await _userService.DeleteUserAsync(id, ct);

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

