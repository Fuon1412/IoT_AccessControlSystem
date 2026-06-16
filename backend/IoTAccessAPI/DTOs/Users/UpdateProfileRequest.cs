namespace IoTAccessAPI.DTOs.Users;

/// <summary>Self profile edit — only fields a user may change about themselves.</summary>
public record UpdateProfileRequest(string? FullName);
