using IoTAccessAPI.Data;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Services;

/// <summary>
/// Periodically derives each device's online/offline state from LastHeartbeat and
/// logs a "connectivity" EventLog ONLY on transition (online↔offline). This is the
/// single source for connectivity events — offline can't be event-driven (a silent
/// device sends nothing), so it must be polled.
/// </summary>
public class DeviceMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceMonitorService> _logger;
    private readonly int _silenceThresholdMinutes;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public DeviceMonitorService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceMonitorService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _silenceThresholdMinutes = config.GetValue("DeviceSilenceThresholdMinutes", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await SweepAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Device connectivity sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = scope.ServiceProvider.GetRequiredService<IEventLogService>();

        var cutoff = DateTime.UtcNow.AddMinutes(-_silenceThresholdMinutes);

        // Active devices that have reported at least once (never_seen → skip until first heartbeat).
        var devices = await db.Devices
            .Where(d => d.IsActive && d.LastHeartbeat != null)
            .Select(d => new { d.Id, d.LastHeartbeat })
            .ToListAsync(ct);

        foreach (var d in devices)
        {
            var desired = d.LastHeartbeat > cutoff ? "online" : "offline";

            // Last connectivity event for this device — log only when state changed.
            var last = await db.EventLogs
                .Where(e => e.DeviceId == d.Id && e.EventType == "connectivity")
                .OrderByDescending(e => e.Timestamp)
                .Select(e => e.Detail)
                .FirstOrDefaultAsync(ct);

            if (last != desired)
                await events.LogAsync(d.Id, "connectivity", desired);
        }
    }
}
