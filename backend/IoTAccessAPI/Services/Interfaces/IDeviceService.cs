using IoTAccessAPI.DTOs.Devices;

namespace IoTAccessAPI.Services.Interfaces;

public interface IDeviceService
{
    Task<IEnumerable<DeviceDto>> GetAllAsync();
    Task<DeviceDto?> GetByIdAsync(int id);
    Task<DeviceStatusDto?> GetStatusAsync(int id);
    /// <summary>Create a device. Returns null if name or MAC already exists.</summary>
    Task<DeviceDto?> CreateAsync(CreateDeviceRequest request);
    Task<DeviceDto?> UpdateAsync(int id, UpdateDeviceRequest request);
    Task<bool> DecommissionAsync(int id);
    Task<bool> UpdateHeartbeatAsync(int id);

    /// <summary>
    /// Resolve a device by name; auto-register it if absent (self-provisioning
    /// for ESP32 nodes). Returns the device id. Updates heartbeat as a side effect.
    /// </summary>
    Task<int> EnsureDeviceByNameAsync(string name);

    /// <summary>
    /// Persist door actuator state ("open"|"closed") reported by firmware servo.
    /// Auto-registers the device if unknown. Returns the updated device, or null.
    /// </summary>
    Task<DeviceDto?> UpdateDoorStateAsync(string name, string doorState);
}
