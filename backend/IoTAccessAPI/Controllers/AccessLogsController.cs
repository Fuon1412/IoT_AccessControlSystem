using System.Security.Claims;
using IoTAccessAPI.DTOs.AccessLogs;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/access-logs")]
public class AccessLogsController : ControllerBase
{
    private readonly IAccessLogService _logService;
    private readonly IUserService _userService;

    public AccessLogsController(IAccessLogService logService, IUserService userService)
    {
        _logService = logService;
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _logService.GetAllAsync();
        return Ok(logs);
    }

    /// <summary>Employee self-service: only the caller's own access history.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetMine()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId))
            return Unauthorized(new { error = "Invalid token" });

        var logs = await _userService.GetAccessLogsAsync(userId);
        return Ok(logs);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Device")]
    public async Task<IActionResult> Create([FromBody] CreateAccessLogRequest request)
    {
        var (log, isDuplicate) = await _logService.CreateAsync(request);

        if (isDuplicate)
            return Ok(log); // idempotent: same RequestId returns existing log, 200 not 201

        return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log);
    }
}
