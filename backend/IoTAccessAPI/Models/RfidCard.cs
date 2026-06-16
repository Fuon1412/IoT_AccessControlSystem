namespace IoTAccessAPI.Models;

public class RfidCard
{
    public int Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Unknown cards are auto-stored on first scan with IsAssigned=false (no user).
    // Admin assigns later → links UserId + sets IsAssigned=true.
    public bool IsAssigned { get; set; } = false;

    public int? UserId { get; set; }
    public User? User { get; set; }
}
