namespace IoTAccessAPI.DTOs.Auth;

public record LoginResponse(string Token, DateTime Expires, string Role);
