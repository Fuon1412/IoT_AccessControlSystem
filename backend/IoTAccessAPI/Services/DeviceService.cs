using IoTAccessAPI.Data;
using IoTAccessAPI.DTOs.Devices;
using IoTAccessAPI.Models;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IoTAccessAPI.Services;

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _db;
    private readonly int _silenceThresholdMinutes;

    public DeviceService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _silenceThresholdMinutes = config.GetValue<int>("DeviceSilenceThresholdMinutes", 5);
    }

    public async Task<IEnumerable<DeviceDto>> GetAllAsync() =>
        await _db.Devices
            .Select(d => ToDto(d))
            .ToListAsync();

    public async Task<DeviceDto?> GetByIdAsync(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        return device is null ? null : ToDto(device);
    }

    public async Task<DeviceStatusDto?> GetStatusAsync(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return null;

        var isOnline = device.LastHeartbeat.HasValue &&
                       device.LastHeartbeat.Value > DateTime.UtcNow.AddMinutes(-_silenceThresholdMinutes);

        var status = !device.IsActive ? "decommissioned"
                   : isOnline ? "online"
                   : device.LastHeartbeat.HasValue ? "offline"
                   : "never_seen";

        return new DeviceStatusDto(device.Id, device.Name, device.LastHeartbeat, isOnline, status);
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceRequest request)
    {
        var device = new Device
        {
            Name = request.Name,
            MacAddress = request.MacAddress,
            Location = request.Location
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return ToDto(device);
    }

    public async Task<DeviceDto?> UpdateAsync(int id, UpdateDeviceRequest request)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return null;

        if (request.Name is not null) device.Name = request.Name;
        if (request.Location is not null) device.Location = request.Location;

        await _db.SaveChangesAsync();
        return ToDto(device);
    }

    public async Task<bool> DecommissionAsync(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return false;

        device.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateHeartbeatAsync(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return false;

        device.LastHeartbeat = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static DeviceDto ToDto(Device d) =>
        new(d.Id, d.Name, d.MacAddress, d.Location, d.IsActive, d.LastHeartbeat, d.CreatedAt);
}
