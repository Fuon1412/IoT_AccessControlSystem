using IoTAccessAPI.DTOs.Cards;

namespace IoTAccessAPI.Services.Interfaces;

public interface IRfidCardService
{
    Task<IEnumerable<RfidCardDto>> GetAllAsync();
    Task<RfidCardDto?> RegisterAsync(RegisterCardRequest request);
    Task<bool> DeactivateAsync(int id);
    Task<CardValidationResult> ValidateAsync(string uid);
}
