namespace IoTAccessAPI.Models;

public class RfidCard
{
    public int Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
