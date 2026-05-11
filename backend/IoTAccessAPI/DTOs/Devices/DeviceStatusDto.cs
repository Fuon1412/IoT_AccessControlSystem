namespace IoTAccessAPI.DTOs.Devices;

public record DeviceStatusDto(int Id, string Name, DateTime? LastHeartbeat, bool IsOnline, string Status);
