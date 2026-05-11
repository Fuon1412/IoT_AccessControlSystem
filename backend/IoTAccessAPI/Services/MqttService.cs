using System.Text;
using System.Text.Json;
using IoTAccessAPI.DTOs.AccessLogs;
using IoTAccessAPI.Hubs;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoTAccessAPI.Services;

public class MqttService : IHostedService, IMqttService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<AccessHub> _hub;
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _config;

    private IMqttClient _client = null!;
    private MqttClientOptions _options = null!;
    private CancellationTokenSource _cts = new();

    public bool IsConnected => _client?.IsConnected ?? false;

    public MqttService(
        IServiceScopeFactory scopeFactory,
        IHubContext<AccessHub> hub,
        ILogger<MqttService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
        _config = config;
    }

    // ── IHostedService ───────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken ct)
    {
        var host = _config["Mqtt:Host"] ?? "localhost";
        var port = _config.GetValue<int>("Mqtt:Port", 1883);
        var clientId = _config["Mqtt:ClientId"] ?? "iot-backend";
        var username = _config["Mqtt:Username"];
        var password = _config["Mqtt:Password"];

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(username))
            optionsBuilder.WithCredentials(username, password);

        _options = optionsBuilder.Build();

        _client = new MqttFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;

        // Connect in background — don't block startup if broker unavailable
        _ = ConnectWithRetryAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _cts.CancelAsync();
        if (_client.IsConnected)
            await _client.DisconnectAsync(cancellationToken: ct);
        _client.Dispose();
    }

    // ── IMqttService ─────────────────────────────────────────────────────────

    public async Task PublishAsync(string topic, string payload, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
        {
            _logger.LogWarning("MQTT publish skipped — not connected. Topic: {Topic}", topic);
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message, ct);
    }

    // ── Connect + subscribe ──────────────────────────────────────────────────

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _client.ConnectAsync(_options, ct);
                _logger.LogInformation("MQTT connected to {Host}:{Port}",
                    _config["Mqtt:Host"], _config["Mqtt:Port"]);

                await SubscribeAsync(ct);
                break;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("MQTT connect failed: {Message}. Retry in 5s", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        await _client.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic("access/+/scan")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), ct);

        await _client.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic("access/+/status")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build(), ct);

        _logger.LogInformation("MQTT subscribed: access/+/scan, access/+/status");
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("MQTT disconnected: {Reason}", args.ReasonString);
        if (!_cts.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
            _ = ConnectWithRetryAsync(_cts.Token);
        }
    }

    // ── Message dispatch ─────────────────────────────────────────────────────

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

        _logger.LogDebug("MQTT [{Topic}] {Payload}", topic, payload);

        try
        {
            var segments = topic.Split('/');
            if (segments.Length != 3) return;

            var deviceTopic = segments[1];

            if (segments[2] == "scan")
                await HandleScanAsync(deviceTopic, payload);
            else if (segments[2] == "status")
                await HandleHeartbeatAsync(deviceTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message on {Topic}", topic);
        }
    }

    // ── Scan handler ─────────────────────────────────────────────────────────
    // Payload: {"device":"esp32-door-01","uid":"A1B2C3D4"}
    // Response: {"access":true,"name":"Nguyen Van A"}

    private async Task HandleScanAsync(string deviceTopic, string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("uid", out var uidEl) ||
            !root.TryGetProperty("device", out var deviceEl))
        {
            _logger.LogWarning("Malformed scan payload on {Topic}: {Payload}", deviceTopic, payload);
            return;
        }

        var uid = uidEl.GetString()!;
        var deviceName = deviceEl.GetString()!;

        using var scope = _scopeFactory.CreateScope();
        var cardService = scope.ServiceProvider.GetRequiredService<IRfidCardService>();
        var logService = scope.ServiceProvider.GetRequiredService<IAccessLogService>();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        // Resolve device
        var allDevices = await deviceService.GetAllAsync();
        var device = allDevices.FirstOrDefault(d =>
            d.Name == deviceName || d.Id.ToString() == deviceTopic);

        if (device is null)
        {
            _logger.LogWarning("Scan from unknown device: {Device}", deviceName);
            return;
        }

        // Validate card
        var validation = await cardService.ValidateAsync(uid);

        // Write log
        var logRequest = new CreateAccessLogRequest(
            RequestId: Guid.NewGuid(),
            RfidUid: uid,
            DeviceId: device.Id,
            AccessGranted: validation.IsValid,
            DenyReason: validation.IsValid ? null : "Card not registered or inactive",
            Timestamp: DateTime.UtcNow);

        await logService.CreateAsync(logRequest); // also broadcasts via SignalR

        // Respond to ESP32
        var response = JsonSerializer.Serialize(new
        {
            access = validation.IsValid,
            name = validation.Username ?? string.Empty
        });

        await PublishAsync($"access/{deviceTopic}/response", response);

        _logger.LogInformation("Scan uid={Uid} device={Device} granted={Granted} user={User}",
            uid, deviceName, validation.IsValid, validation.Username ?? "-");
    }

    // ── Heartbeat handler ────────────────────────────────────────────────────

    private async Task HandleHeartbeatAsync(string deviceTopic)
    {
        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        if (int.TryParse(deviceTopic, out var deviceId))
        {
            await deviceService.UpdateHeartbeatAsync(deviceId);
        }
        else
        {
            var devices = await deviceService.GetAllAsync();
            var device = devices.FirstOrDefault(d => d.Name == deviceTopic);
            if (device is not null)
                await deviceService.UpdateHeartbeatAsync(device.Id);
        }
    }
}
