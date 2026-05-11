using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.AccessLogs;

public record CreateAccessLogRequest(
    [Required] Guid RequestId,
    [Required, StringLength(50)] string RfidUid,
    [Required] int DeviceId,
    bool AccessGranted,
    string? DenyReason,
    DateTime? Timestamp);
