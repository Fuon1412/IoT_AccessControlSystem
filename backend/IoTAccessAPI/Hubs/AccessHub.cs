using Microsoft.AspNetCore.SignalR;

namespace IoTAccessAPI.Hubs;

/// <summary>
/// Dashboard clients connect here to receive real-time access log events.
/// Server calls Clients.All.SendAsync("NewAccessLog", log) on each new POST.
/// </summary>
public class AccessHub : Hub
{
}
