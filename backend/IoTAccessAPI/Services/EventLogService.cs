using IoTAccessAPI.Data;
using IoTAccessAPI.DTOs.EventLogs;
using IoTAccessAPI.Hubs;
using IoTAccessAPI.Models;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Services;

public class EventLogService : IEventLogService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AccessHub> _hub;

    public EventLogService(AppDbContext db, IHubContext<AccessHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<IEnumerable<EventLogDto>> GetAllAsync() =>
        await _db.EventLogs
            .Include(e => e.Device)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => ToDto(e))
            .ToListAsync();

    public async Task<EventLogDto> LogAsync(int deviceId, string eventType, string detail, string? actor = null)
    {
        var entity = new EventLog
        {
            DeviceId = deviceId,
            EventType = eventType,
            Detail = detail,
            Actor = actor,
            Timestamp = DateTime.UtcNow,
        };

        _db.EventLogs.Add(entity);
        await _db.SaveChangesAsync();
        await _db.Entry(entity).Reference(e => e.Device).LoadAsync();

        var dto = ToDto(entity);
        await _hub.Clients.All.SendAsync("NewEventLog", dto);
        return dto;
    }

    private static EventLogDto ToDto(EventLog e) =>
        new(e.Id, e.EventType, e.Detail, e.Actor, e.Timestamp, e.DeviceId, e.Device.Name);
}
