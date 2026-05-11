namespace IoTAccessAPI.Models;

public class AccessLog
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }   // idempotency key from ESP32
    public string RfidUid { get; set; } = string.Empty;
    public bool AccessGranted { get; set; }
    public string? DenyReason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int? UserId { get; set; }
    public User? User { get; set; }
}
