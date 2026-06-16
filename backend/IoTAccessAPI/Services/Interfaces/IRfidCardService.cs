using IoTAccessAPI.DTOs.Cards;

namespace IoTAccessAPI.Services.Interfaces;

public interface IRfidCardService
{
    Task<IEnumerable<RfidCardDto>> GetAllAsync();
    Task<IEnumerable<RfidCardDto>> GetUnassignedAsync();
    Task<RfidCardDto?> RegisterAsync(RegisterCardRequest request);
    Task<RfidCardDto?> AssignAsync(int cardId, int userId);
    Task<bool> DeactivateAsync(int id);
    Task<CardValidationResult> ValidateAsync(string uid);

    /// <summary>
    /// Ensure an unknown UID is persisted. On first sight, stores it unassigned
    /// (IsAssigned=false, no user). Idempotent — no duplicate rows on rescan.
    /// </summary>
    Task EnsureCardExistsAsync(string uid);
}
