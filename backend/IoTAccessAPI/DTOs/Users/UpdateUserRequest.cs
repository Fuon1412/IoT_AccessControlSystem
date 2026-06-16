namespace IoTAccessAPI.DTOs.Users;

public record UpdateUserRequest(string? Username, string? FullName, string? Role, bool? IsActive);
