namespace IoTAccessAPI.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty; // display name (OLED + UI)
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee"; // Admin, Employee, Device
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
