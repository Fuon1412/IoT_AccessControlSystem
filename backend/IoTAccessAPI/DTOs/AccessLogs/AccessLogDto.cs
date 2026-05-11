namespace IoTAccessAPI.DTOs.AccessLogs;

public record AccessLogDto(
    int Id,
    Guid RequestId,
    string RfidUid,
    bool AccessGranted,
    string? DenyReason,
    DateTime Timestamp,
    int DeviceId,
    string DeviceName,
    int? UserId,
    string? Username);
