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

    public AccessLogsController(IAccessLogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _logService.GetAllAsync();
        return Ok(new { logs, total = logs.Count() });
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
