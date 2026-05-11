namespace IoTAccessAPI.DTOs.Users;

public record UserDto(int Id, string Username, string Role, bool IsActive, DateTime CreatedAt);
