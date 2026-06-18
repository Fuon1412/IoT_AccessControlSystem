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

    public async Task<DeviceDto?> CreateAsync(CreateDeviceRequest request)
    {
        // Guard unique constraints — return null → controller responds 409 (not 500).
        if (await _db.Devices.AnyAsync(d => d.Name == request.Name))
            return null;
        if (!string.IsNullOrWhiteSpace(request.MacAddress)
            && await _db.Devices.AnyAsync(d => d.MacAddress == request.MacAddress))
            return null;

        var device = new Device
        {
            Name = request.Name,
            MacAddress = request.MacAddress ?? string.Empty,
            Location = request.Location ?? string.Empty
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return ToDto(device);
    }

    public async Task<int> EnsureDeviceByNameAsync(string name)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Name == name);
        if (device is null)
        {
            // Self-provision: ESP32 reported a name we don't know → register it.
            // MAC unknown at this point — use a unique placeholder derived from name.
            device = new Device
            {
                Name = name,
                MacAddress = $"auto:{name}",
                Location = "Auto-registered",
                IsActive = true,
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
        }

        device.LastHeartbeat = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return device.Id;
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

    public async Task<DeviceDto?> UpdateDoorStateAsync(string name, string doorState)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Name == name);
        if (device is null)
        {
            // Door event from an unknown node → self-provision (mirror EnsureDeviceByNameAsync).
            device = new Device
            {
                Name = name,
                MacAddress = $"auto:{name}",
                Location = "Auto-registered",
                IsActive = true,
            };
            _db.Devices.Add(device);
        }

        device.DoorState = doorState;
        device.LastDoorStateChange = DateTime.UtcNow;
        device.LastHeartbeat = DateTime.UtcNow;   // door event also proves liveness
        await _db.SaveChangesAsync();
        return ToDto(device);
    }

    private static DeviceDto ToDto(Device d) =>
        new(d.Id, d.Name, d.MacAddress, d.Location, d.IsActive, d.LastHeartbeat, d.DoorState, d.LastDoorStateChange, d.CreatedAt);
}
