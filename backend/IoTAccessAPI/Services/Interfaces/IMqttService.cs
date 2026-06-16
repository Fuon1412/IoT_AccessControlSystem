namespace IoTAccessAPI.Services.Interfaces;

public interface IMqttService
{
    Task PublishAsync(string topic, string payload, CancellationToken ct = default);

    /// <summary>Send a command to a device: publishes to {prefix}/devices/{name}/command.</summary>
    Task PublishCommandAsync(string deviceName, string payload, CancellationToken ct = default);

    bool IsConnected { get; }
}
