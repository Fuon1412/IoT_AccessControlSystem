using IoTAccessAPI.DTOs.Users;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userService.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var result = await _userService.CreateAsync(request);
        if (result is null)
            return Conflict(new { error = "Username already exists" });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Create an Employee account (can only view own access history).
    /// Optionally links an RFID card UID in the same call.</summary>
    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateUserRequest request)
    {
        var employee = request with { Role = "Employee" };
        var result = await _userService.CreateAsync(employee);
        if (result is null)
            return Conflict(new { error = "Username already exists" });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        if (await _userService.GetByIdAsync(id) is null)
            return NotFound();

        var result = await _userService.UpdateAsync(id, request);
        return result is null ? Conflict(new { error = "Username already exists" }) : Ok(result);
    }

    /// <summary>Admin resets a user's password (no current-password required).</summary>
    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var ok = await _userService.ResetPasswordAsync(id, request.NewPassword);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _userService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("{id:int}/access-logs")]
    public async Task<IActionResult> GetAccessLogs(int id)
    {
        var logs = await _userService.GetAccessLogsAsync(id);
        return Ok(logs);
    }
}
