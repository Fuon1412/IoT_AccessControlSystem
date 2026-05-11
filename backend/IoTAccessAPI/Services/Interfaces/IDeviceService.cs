using IoTAccessAPI.DTOs.Devices;

namespace IoTAccessAPI.Services.Interfaces;

public interface IDeviceService
{
    Task<IEnumerable<DeviceDto>> GetAllAsync();
    Task<DeviceDto?> GetByIdAsync(int id);
    Task<DeviceStatusDto?> GetStatusAsync(int id);
    Task<DeviceDto> CreateAsync(CreateDeviceRequest request);
    Task<DeviceDto?> UpdateAsync(int id, UpdateDeviceRequest request);
    Task<bool> DecommissionAsync(int id);
    Task<bool> UpdateHeartbeatAsync(int id);
}
