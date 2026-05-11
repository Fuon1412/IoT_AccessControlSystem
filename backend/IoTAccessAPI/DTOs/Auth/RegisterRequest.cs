namespace IoTAccessAPI.DTOs.Auth;

public record RegisterRequest(string Username, string Password, string? Role);
