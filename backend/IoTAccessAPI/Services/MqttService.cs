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
    private string _endpoint = "";   // for logging — ws uri or host:port

    // Optional topic prefix. Private HiveMQ cluster needs none — left empty.
    // Set Mqtt:TopicPrefix only if sharing a broker namespace. Must match firmware.
    private string _prefix = "";

    // Prepend prefix only when set → "iot7f3a/access/..." or plain "access/...".
    private string Topic(string rest) => string.IsNullOrEmpty(_prefix) ? rest : $"{_prefix}/{rest}";

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
        _prefix = (_config["Mqtt:TopicPrefix"] ?? "").Trim().TrimEnd('/');
        var clientId = _config["Mqtt:ClientId"] ?? "iot-backend";
        var username = _config["Mqtt:Username"];
        var password = _config["Mqtt:Password"];
        var transport = _config["Mqtt:Transport"] ?? "tcp";   // "websocket" | "tcp"
        var useTls = _config.GetValue("Mqtt:UseTls", false);

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithCleanSession();

        // TLS SNI target — HiveMQ Cloud (multi-tenant) routes by SNI; missing it
        // = LB drops the handshake ("unexpected EOF"). Set explicitly from host/uri.
        string tlsHost;

        // Transport — secure broker is WebSocket-Secure (wss). TCP kept for local dev.
        if (string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase))
        {
            var wsUri = _config["Mqtt:WebSocketUri"]
                ?? throw new InvalidOperationException("Mqtt:WebSocketUri required when Mqtt:Transport=websocket");
            optionsBuilder.WithWebSocketServer(o => o.WithUri(wsUri));
            _endpoint = (useTls ? "wss://" : "ws://") + wsUri;
            tlsHost = wsUri.Split('/')[0].Split(':')[0];   // strip port + path
        }
        else
        {
            var host = _config["Mqtt:Host"] ?? "localhost";
            var port = _config.GetValue<int>("Mqtt:Port", 1883);
            optionsBuilder.WithTcpServer(host, port);
            _endpoint = $"{host}:{port}";
            tlsHost = host;
        }

        if (useTls)
            optionsBuilder.WithTlsOptions(o => o
                .UseTls()
                .WithTargetHost(tlsHost)
                .WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13));

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
        => PublishAsync(Topic($"devices/{deviceName}/command"), payload, ct);

    // ── Connect + subscribe ──────────────────────────────────────────────────

    // Guard against overlapping reconnect loops — both startup and the disconnect
    // handler call this; without it MQTTnet throws "connect while pending".
    private int _connecting;

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _connecting, 1) == 1) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_client.IsConnected) break;
                    await _client.ConnectAsync(_options, ct);
                    _logger.LogInformation("MQTT connected to {Endpoint}", _endpoint);

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
        finally
        {
            Interlocked.Exchange(ref _connecting, 0);
        }
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        await _client.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(Topic("access/+/scan"))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), ct);

        await _client.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(Topic("access/+/status"))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build(), ct);

        _logger.LogInformation("MQTT subscribed: {Scan}, {Status}", Topic("access/+/scan"), Topic("access/+/status"));
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
            // Topic: [{prefix}/]access/{device}/{verb}
            var segments = topic.Split('/');
            var i = 0;
            if (!string.IsNullOrEmpty(_prefix))
            {
                if (segments.Length < 1 || segments[0] != _prefix) return;
                i = 1;   // skip prefix segment
            }
            if (segments.Length != i + 3) return;
            if (segments[i] != "access") return;

            var deviceTopic = segments[i + 1];
            var verb = segments[i + 2];

            if (verb == "scan")
                await HandleScanAsync(deviceTopic, payload);
            else if (verb == "status")
                await HandleStatusAsync(deviceTopic, payload);
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

        await PublishAsync(Topic($"access/{deviceTopic}/response"), response);

        _logger.LogInformation("Scan uid={Uid} device={Device} granted={Granted} user={User}",
            uid, deviceName, validation.IsValid, validation.Username ?? "-");
    }

    // ── Status handler ───────────────────────────────────────────────────────
    // Two payload shapes share the status topic:
    //   "online"                                     → heartbeat (plain string)
    //   {"device":"...","door":"open"|"closed"}      → door actuator state
    private async Task HandleStatusAsync(string deviceTopic, string payload)
    {
        var trimmed = payload.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("door", out var doorEl))
                {
                    await HandleDoorStateAsync(deviceTopic, doorEl.GetString());
                    return;
                }
            }
            catch (JsonException)
            {
                // Malformed JSON → fall through, treat as heartbeat.
            }
        }

        await HandleHeartbeatAsync(deviceTopic);
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

    // ── Door state handler ───────────────────────────────────────────────────
    // Persist current door state on the device + push live "DoorStateChanged".
    private async Task HandleDoorStateAsync(string deviceTopic, string? doorState)
    {
        if (doorState is not ("open" or "closed"))
        {
            _logger.LogWarning("Unknown door state '{State}' from {Device}", doorState, deviceTopic);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var device = await deviceService.UpdateDoorStateAsync(deviceTopic, doorState);
        if (device is null) return;

        // Persist the actuation as a device event (also broadcasts "NewEventLog").
        var eventLog = scope.ServiceProvider.GetRequiredService<IEventLogService>();
        await eventLog.LogAsync(device.Id, "door", doorState);

        await _hub.Clients.All.SendAsync("DoorStateChanged", new
        {
            deviceId = device.Id,
            deviceName = device.Name,
            doorState,
            timestamp = DateTime.UtcNow,
        });

        _logger.LogInformation("Door {State} on {Device}", doorState, device.Name);
    }
}
