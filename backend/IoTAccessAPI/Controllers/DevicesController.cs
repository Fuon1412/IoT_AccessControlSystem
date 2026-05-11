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

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _deviceService.GetAllAsync();
        return Ok(new { devices, total = devices.Count() });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _deviceService.GetByIdAsync(id);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpGet("{id:int}/status")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var status = await _deviceService.GetStatusAsync(id);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest request)
    {
        var device = await _deviceService.CreateAsync(request);
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
}
