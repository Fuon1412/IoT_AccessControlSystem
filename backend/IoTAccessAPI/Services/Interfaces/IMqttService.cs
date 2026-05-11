namespace IoTAccessAPI.Services.Interfaces;

public interface IMqttService
{
    Task PublishAsync(string topic, string payload, CancellationToken ct = default);
    bool IsConnected { get; }
}
