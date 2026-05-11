using IoTAccessAPI.Data;
using IoTAccessAPI.DTOs.AccessLogs;
using IoTAccessAPI.Hubs;
using IoTAccessAPI.Models;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Services;

public class AccessLogService : IAccessLogService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AccessHub> _hub;

    public AccessLogService(AppDbContext db, IHubContext<AccessHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<IEnumerable<AccessLogDto>> GetAllAsync() =>
        await _db.AccessLogs
            .Include(a => a.Device)
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => ToDto(a))
            .ToListAsync();

    public async Task<IEnumerable<AccessLogDto>> GetByUserIdAsync(int userId) =>
        await _db.AccessLogs
            .Include(a => a.Device)
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => ToDto(a))
            .ToListAsync();

    public async Task<(AccessLogDto log, bool isDuplicate)> CreateAsync(CreateAccessLogRequest request)
    {
        // Idempotency: return existing log if RequestId already seen
        var existing = await _db.AccessLogs
            .Include(a => a.Device)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.RequestId == request.RequestId);

        if (existing is not null)
            return (ToDto(existing), true);

        // Resolve RFID UID to user (if card registered)
        var card = await _db.RfidCards
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Uid == request.RfidUid && c.IsActive);

        var log = new AccessLog
        {
            RequestId = request.RequestId,
            RfidUid = request.RfidUid,
            DeviceId = request.DeviceId,
            AccessGranted = request.AccessGranted,
            DenyReason = request.DenyReason,
            Timestamp = request.Timestamp?.ToUniversalTime() ?? DateTime.UtcNow,
            UserId = card?.UserId
        };

        _db.AccessLogs.Add(log);
        await _db.SaveChangesAsync();
        await _db.Entry(log).Reference(l => l.Device).LoadAsync();

        var dto = ToDto(log);

        // Broadcast to all SignalR dashboard clients
        await _hub.Clients.All.SendAsync("NewAccessLog", dto);

        return (dto, false);
    }

    private static AccessLogDto ToDto(AccessLog a) =>
        new(a.Id, a.RequestId, a.RfidUid, a.AccessGranted, a.DenyReason,
            a.Timestamp, a.DeviceId, a.Device.Name, a.UserId, a.User?.Username);
}
