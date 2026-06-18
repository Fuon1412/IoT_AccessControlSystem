using IoTAccessAPI.DTOs.EventLogs;

namespace IoTAccessAPI.Services.Interfaces;

public interface IEventLogService
{
    Task<IEnumerable<EventLogDto>> GetAllAsync();

    /// <summary>Persist a device event + broadcast "NewEventLog" over SignalR.</summary>
    Task<EventLogDto> LogAsync(int deviceId, string eventType, string detail, string? actor = null);
}
