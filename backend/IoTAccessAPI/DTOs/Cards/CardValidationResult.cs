namespace IoTAccessAPI.DTOs.Cards;

public record CardValidationResult(
    bool IsValid, int? UserId, string? Username, string? DisplayName, string? Role);
