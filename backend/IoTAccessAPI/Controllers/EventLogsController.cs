using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/event-logs")]
public class EventLogsController : ControllerBase
{
    private readonly IEventLogService _eventLogService;

    public EventLogsController(IEventLogService eventLogService)
    {
        _eventLogService = eventLogService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var events = await _eventLogService.GetAllAsync();
        return Ok(events);
    }
}
