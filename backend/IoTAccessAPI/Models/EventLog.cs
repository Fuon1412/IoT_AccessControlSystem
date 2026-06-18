namespace IoTAccessAPI.Models;

/// <summary>
/// Device lifecycle event — distinct from AccessLog (RFID scans).
/// Persists what previously was only broadcast over SignalR:
///   EventType "door"         → Detail "open" | "closed"      (firmware servo)
///   EventType "connectivity" → Detail "online" | "offline"   (heartbeat monitor)
///   EventType "emergency"    → Detail "lock" | "unlock", Actor = admin username
/// </summary>
public class EventLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Actor { get; set; }   // who triggered (emergency); null for device-originated
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
}
