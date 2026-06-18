namespace IoTAccessAPI.DTOs.Devices;

public record DeviceDto(int Id, string Name, string MacAddress, string Location, bool IsActive, DateTime? LastHeartbeat, string DoorState, DateTime? LastDoorStateChange, DateTime CreatedAt);
