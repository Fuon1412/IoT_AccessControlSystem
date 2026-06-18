namespace IoTAccessAPI.DTOs.EventLogs;

public record EventLogDto(
    int Id,
    string EventType,
    string Detail,
    string? Actor,
    DateTime Timestamp,
    int DeviceId,
    string DeviceName);
