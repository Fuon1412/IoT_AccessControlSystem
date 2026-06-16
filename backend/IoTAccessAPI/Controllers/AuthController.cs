using System.Security.Claims;
using IoTAccessAPI.DTOs.Auth;
using IoTAccessAPI.DTOs.Users;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result is null)
            return Unauthorized(new { error = "Invalid credentials" });

        return Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (result is null)
            return Conflict(new { error = "Username already exists" });

        return CreatedAtAction(nameof(Register), new { id = result.Id }, result);
    }

    /// <summary>Current user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();
        var user = await _userService.GetByIdAsync(userId);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Update own profile (full name only — role/active are admin-managed).</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

        var result = await _userService.UpdateAsync(userId,
            new UpdateUserRequest(null, request.FullName, null, null));
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Authenticated user changes their own password (verifies current).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId))
            return Unauthorized(new { error = "Invalid token" });

        var ok = await _userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        return ok ? NoContent() : BadRequest(new { error = "Current password is incorrect" });
    }
}
