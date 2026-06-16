using System.Security.Claims;
using System.Text.Json;
using IoTAccessAPI.DTOs.Devices;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize(Roles = "Admin")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IMqttService _mqtt;
    private readonly IAuthService _auth;

    public DevicesController(IDeviceService deviceService, IMqttService mqtt, IAuthService auth)
    {
        _deviceService = deviceService;
        _mqtt = mqtt;
        _auth = auth;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _deviceService.GetAllAsync();
        return Ok(devices);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _deviceService.GetByIdAsync(id);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpGet("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var status = await _deviceService.GetStatusAsync(id);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        var device = await _deviceService.CreateAsync(request);
        if (device is null)
            return Conflict(new { error = "Device name or MAC address already exists" });

        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceRequest request)
    {
        var result = await _deviceService.UpdateAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Decommission(int id)
    {
        var success = await _deviceService.DecommissionAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/heartbeat")]
    [Authorize(Roles = "Admin,Device")]
    public async Task<IActionResult> Heartbeat(int id)
    {
        var success = await _deviceService.UpdateHeartbeatAsync(id);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Emergency force-lock / force-unlock a door. Admin-only, re-confirms password,
    /// then publishes a command over MQTT to the device.
    /// </summary>
    [HttpPost("{id:int}/emergency")]
    public async Task<IActionResult> Emergency(int id, [FromBody] EmergencyCommandRequest request)
    {
        var action = request.Action?.ToLowerInvariant();
        if (action != "lock" && action != "unlock")
            return BadRequest(new { error = "Action must be 'lock' or 'unlock'" });

        // Re-confirm the calling admin's password.
        var username = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        if (!await _auth.VerifyPasswordAsync(username, request.Password))
            return Unauthorized(new { error = "Password confirmation failed" });

        var device = await _deviceService.GetByIdAsync(id);
        if (device is null) return NotFound(new { error = "Device not found" });

        var payload = JsonSerializer.Serialize(new { command = action, by = username });
        await _mqtt.PublishCommandAsync(device.Name, payload);

        return Ok(new { sent = true, device = device.Name, action });
    }
}
