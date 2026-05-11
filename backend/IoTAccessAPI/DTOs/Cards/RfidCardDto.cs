namespace IoTAccessAPI.DTOs.Cards;

public record RfidCardDto(int Id, string Uid, bool IsActive, DateTime RegisteredAt, int UserId, string Username);
