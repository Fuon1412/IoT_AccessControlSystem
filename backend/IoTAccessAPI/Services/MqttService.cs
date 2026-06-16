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

    // Topic prefix — shared public broker requires a unique namespace.
    // Must match firmware TOPIC_PREFIX. Config: Mqtt:TopicPrefix (default iot7f3a).
    private string _prefix = "iot7f3a";

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
        _prefix = (_config["Mqtt:TopicPrefix"] ?? "iot7f3a").TrimEnd('/');
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

    public Task PublishCommandAsync(string deviceName, string payload, CancellationToken ct = default)
        => PublishAsync($"{_prefix}/devices/{deviceName}/command", payload, ct);

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
            .WithTopic($"{_prefix}/access/+/scan")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), ct);

        await _client.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic($"{_prefix}/access/+/status")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build(), ct);

        _logger.LogInformation("MQTT subscribed: {Prefix}/access/+/scan, {Prefix}/access/+/status", _prefix, _prefix);
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
            // Topic: {prefix}/access/{device}/{verb}
            var segments = topic.Split('/');
            if (segments.Length != 4) return;
            if (segments[0] != _prefix || segments[1] != "access") return;

            var deviceTopic = segments[2];
            var verb = segments[3];

            if (verb == "scan")
                await HandleScanAsync(deviceTopic, payload);
            else if (verb == "status")
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

        // Resolve device — auto-register if unknown (self-provisioning).
        var deviceId = await deviceService.EnsureDeviceByNameAsync(deviceName);

        // Validate card
        var validation = await cardService.ValidateAsync(uid);

        // Denied → auto-store the UID (unassigned) so admin can assign it later.
        // Idempotent: no duplicate row if already known.
        if (!validation.IsValid)
            await cardService.EnsureCardExistsAsync(uid);

        // Write log
        var logRequest = new CreateAccessLogRequest(
            RequestId: Guid.NewGuid(),
            RfidUid: uid,
            DeviceId: deviceId,
            AccessGranted: validation.IsValid,
            DenyReason: validation.IsValid ? null : "Card not assigned or inactive",
            Timestamp: DateTime.UtcNow);

        await logService.CreateAsync(logRequest); // also broadcasts via SignalR

        // Respond to ESP32
        var response = JsonSerializer.Serialize(new
        {
            access = validation.IsValid,
            name = validation.DisplayName ?? validation.Username ?? string.Empty
        });

        await PublishAsync($"{_prefix}/access/{deviceTopic}/response", response);

        _logger.LogInformation("Scan uid={Uid} device={Device} granted={Granted} user={User}",
            uid, deviceName, validation.IsValid, validation.Username ?? "-");
    }

    // ── Heartbeat handler ────────────────────────────────────────────────────

    private async Task HandleHeartbeatAsync(string deviceTopic)
    {
        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        // deviceTopic is the device name (firmware DEVICE_ID). Auto-register if
        // unknown so a fresh ESP32 appears in the registry on its first heartbeat.
        await deviceService.EnsureDeviceByNameAsync(deviceTopic);
    }
}
