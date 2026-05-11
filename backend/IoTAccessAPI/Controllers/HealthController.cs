using IoTAccessAPI.Data;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDeviceService _deviceService;
    private readonly int _silenceThresholdMinutes;

    public HealthController(AppDbContext db, IDeviceService deviceService, IConfiguration config)
    {
        _db = db;
        _deviceService = deviceService;
        _silenceThresholdMinutes = config.GetValue<int>("DeviceSilenceThresholdMinutes", 5);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        bool dbOk;
        try { dbOk = await _db.Database.CanConnectAsync(); }
        catch { dbOk = false; }

        return Ok(new
        {
            status = dbOk ? "ok" : "degraded",
            database = dbOk ? "connected" : "unreachable",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDeviceHealth()
    {
        var devices = await _db.Devices
            .Where(d => d.IsActive)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.LastHeartbeat,
                IsOnline = d.LastHeartbeat != null &&
                           d.LastHeartbeat > DateTime.UtcNow.AddMinutes(-_silenceThresholdMinutes),
                SilentAlert = d.LastHeartbeat != null &&
                              d.LastHeartbeat < DateTime.UtcNow.AddMinutes(-_silenceThresholdMinutes)
            })
            .ToListAsync();

        return Ok(new
        {
            devices,
            silenceThresholdMinutes = _silenceThresholdMinutes,
            timestamp = DateTime.UtcNow
        });
    }
}
