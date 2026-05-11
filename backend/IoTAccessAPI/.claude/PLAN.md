# IoT Access Control — Master Plan

CLAUDE.md updated with MQTT architecture. Phases 1–4 done. New phases below.

## Done ✓

| Phase | What |
|---|---|
| 1 | JWT auth + BCrypt + role-based access (Admin/User/Device) |
| 2 | SignalR hub `/hubs/access` — real-time push to dashboard |
| 3 | Admin API — user/device/card CRUD, `/api/cards/validate/{uid}` |
| 4 | Idempotent access logs (RequestId), health endpoint + device silence alert |
| — | Controller-interface-service refactor |
| — | Docker: Dockerfiles + docker-compose + nginx proxy |

---

## Phase 5: MQTT Infrastructure

**Goal**: Broker + backend plumbing before any ESP32 swap.

### 5.1 Mosquitto in Docker

Add to `docker-compose.yml`:
```yaml
mosquitto:
  image: eclipse-mosquitto:2
  ports:
    - "1883:1883"
  volumes:
    - ./mosquitto/mosquitto.conf:/mosquitto/config/mosquitto.conf
    - mosquitto_data:/mosquitto/data
  healthcheck:
    test: ["CMD", "mosquitto_sub", "-t", "$$SYS/#", "-C", "1", "-i", "healthcheck"]
    interval: 10s
    retries: 5
```

Add `mosquitto_data` to volumes block. Backend `depends_on` mosquitto healthy.

### 5.2 mosquitto.conf

`mosquitto/mosquitto.conf`:
```
listener 1883
allow_anonymous true        # dev only — set false + add password_file for prod

persistence true
persistence_location /mosquitto/data/
log_dest stdout
```

Prod hardening (Phase 5.3):
```
allow_anonymous false
password_file /mosquitto/config/passwd
```
Generate: `mosquitto_passwd -c passwd <username>`

### 5.3 MQTTnet Package

```bash
cd backend/IoTAccessAPI
dotnet add package MQTTnet --version 4.3.7
```

Add to `appsettings.json`:
```json
"Mqtt": {
  "Host": "localhost",
  "Port": 1883,
  "ClientId": "iot-backend",
  "Username": "",
  "Password": ""
}
```

Docker override in compose env:
```
Mqtt__Host=mosquitto
Mqtt__Port=1883
```

---

## Phase 6: Backend MqttService

**Goal**: Backend subscribes broker, processes scans, pushes SignalR.

### 6.1 IMqttService Interface

`Services/Interfaces/IMqttService.cs`:
- `Task PublishAsync(string topic, string payload)`
- `Task SubscribeAsync(string topicFilter)`

### 6.2 MqttService BackgroundService

`Services/MqttService.cs` — `IHostedService` + `IMqttService`:

```
Connect to broker on startup
Subscribe: access/+/scan
Subscribe: access/+/status  (heartbeat)

On access/{deviceId}/scan message:
  1. Parse payload: { "device": "...", "uid": "..." }
  2. Validate UID → IRfidCardService.ValidateAsync(uid)
  3. Write AccessLog → IAccessLogService (reuse existing)
  4. Publish response → access/{deviceId}/response
     payload: { "access": true/false, "name": "..." }
  5. SignalR broadcast → _hub.Clients.All.SendAsync("NewAccessLog", log)

On access/{deviceId}/status message:
  1. Parse deviceId from topic
  2. IDeviceService.UpdateHeartbeatAsync(deviceId)
```

Payload schemas (match CLAUDE.md exactly):
```json
// ESP32 → broker (scan)
{"device": "esp32-door-01", "uid": "A1B2C3D4"}

// Backend → ESP32 (response)  
{"access": true, "name": "Nguyen Van A"}
```

### 6.3 Register in Program.cs

```csharp
builder.Services.AddSingleton<IMqttService, MqttService>();
builder.Services.AddHostedService(sp => (MqttService)sp.GetRequiredService<IMqttService>());
```

Singleton (not scoped) — one persistent broker connection per app lifetime.
Inject `IServiceScopeFactory` inside MqttService for scoped DB access per message.

### 6.4 Topic Map

| Topic | Direction | Handler |
|---|---|---|
| `access/{deviceId}/scan` | ESP32 → backend | Validate + log + respond |
| `access/{deviceId}/response` | Backend → ESP32 | Publish only |
| `access/{deviceId}/status` | ESP32 → backend | UpdateHeartbeat |
| `devices/{deviceId}/command` | Backend → ESP32 | Publish only (future) |

---

## Phase 7: ESP32 MQTT Migration

**Goal**: Replace HTTP POST with MQTT publish. Drop hardcoded UID map.

### 7.1 Libraries (Arduino/PlatformIO)

```ini
# platformio.ini
lib_deps =
  knolleary/PubSubClient @ ^2.8
  bblanchon/ArduinoJson @ ^7.0
```

### 7.2 Flow Change

Old:
```
Scan UID → GET /api/cards/validate/{uid} → POST /api/access-logs
```

New:
```
Scan UID → mqtt.publish("access/{deviceId}/scan", payload)
         → wait for response on "access/{deviceId}/response"
         → grant/deny door + OLED + buzzer
```

### 7.3 ESP32 Implementation Notes

- `mqtt.loop()` every `loop()` iteration — blocks reconnect if omitted
- Subscribe `access/{deviceId}/response` in `reconnect()` after each connect
- Callback `onMqttMessage(topic, payload)` → parse JSON → actuate
- Heartbeat: `mqtt.publish("access/{deviceId}/status", "online")` every 60s
- **No REST calls needed** — broker handles all device comms

### 7.4 SPIFFS Retry Queue

On publish fail (broker down):
1. Serialize `{uid, timestamp, deviceId}` to SPIFFS file (append)
2. On reconnect: read SPIFFS, flush queued publishes, delete file
3. Limit queue: 50 entries max — discard oldest on overflow

### 7.5 Device Auth on Broker (prod)

ESP32 connects with credentials:
```cpp
client.connect(deviceId, MQTT_USER, MQTT_PASS);
```
Store in `config.h` or NVS — never hardcode in shared firmware.

---

## Phase 8: Broker Auth (Prod Hardening)

After MQTT working in dev:

1. `mosquitto_passwd -c mosquitto/passwd backend-service`
2. `mosquitto_passwd -b mosquitto/passwd esp32-door-01 <pass>`
3. Set `allow_anonymous false` in `mosquitto.conf`
4. Add creds to backend `appsettings.json` (`Mqtt:Username`, `Mqtt:Password`)
5. Flash ESP32 with credentials in NVS (not firmware)

---

## File Impact Map

| File | Phase | Change |
|---|---|---|
| `docker-compose.yml` | 5.1 | Add mosquitto service + volume |
| `mosquitto/mosquitto.conf` | 5.1 | New file |
| `IoTAccessAPI.csproj` | 5.3 | Add MQTTnet package |
| `appsettings.json` | 5.3 | Add Mqtt config block |
| `Services/Interfaces/IMqttService.cs` | 6.1 | New interface |
| `Services/MqttService.cs` | 6.2 | New BackgroundService |
| `Program.cs` | 6.3 | Register singleton + hosted service |
| `esp32_logic/main.cpp` | 7.2–7.4 | Replace HTTP with MQTT |

---

## Execution Order

```
5.1 → 5.2 → 5.3 → 6.1 → 6.2 → 6.3 → test broker ↔ backend
→ 7.1 → 7.2 → 7.3 → 7.4 → test ESP32 ↔ broker ↔ backend
→ 8 (prod only)
```

Broker must be reachable before MqttService starts — `depends_on: mosquitto: condition: service_healthy`.
Test backend MQTT without ESP32: `mosquitto_pub -h localhost -t "access/esp32-door-01/scan" -m '{"device":"esp32-door-01","uid":"A1B2C3D4"}'`
