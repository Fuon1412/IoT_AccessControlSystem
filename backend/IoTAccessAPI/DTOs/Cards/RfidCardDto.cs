namespace IoTAccessAPI.DTOs.Cards;

public record RfidCardDto(
    int Id,
    string Uid,
    bool IsActive,
    bool IsAssigned,
    DateTime RegisteredAt,
    int? UserId,
    string? Username);
