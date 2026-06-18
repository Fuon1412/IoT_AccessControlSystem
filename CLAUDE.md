## Communication Style
Caveman terse. Tech exact. Fluff die.
Drop: articles, filler, pleasantries, hedging.
Fragments OK. Short synonyms. Code unchanged.
ACTIVE EVERY RESPONSE. No revert.
Off: "stop caveman" / "normal mode".

# IoT Access Control System - Project Documentation
# Project Insight — IoT Access Control System

## Stack Decisions

- **Backend .NET 8** — strong typing, EF Core migrations handle schema clean, Swagger auto-gen, no extra setup
- **Frontend React 19 + TanStack Router** — file-based routing, no boilerplate, pair with Vite HMR
- **ESP32** — Wi-Fi built-in, enough GPIO for RFID + OLED + buzzer, 3.3V logic match RC522
- **MQTT (implemented)** — replaced HTTP POST, bidirectional, auto-reconnect, lower latency. ESP32 firmware publishes scan + subscribes response.

## Data Flow

```
Active (MQTT) — firmware AccessControl.ino:
ESP32 scan → publish access/{deviceId}/scan → Mosquitto broker
Broker → MqttService (BackgroundService) → DB write + publish access/{deviceId}/response
ESP32 ← response {"access":bool,"name":"..."} ← broker
  → GRANTED: OLED shows user name + short buzzer beep
  → DENIED : OLED "DENIED!" + 3 alarm beeps
Offline/timeout → ESP32 buffers scan in SPIFFS queue, flushes on reconnect
Frontend ← SignalR push from MqttService (planned; currently REST poll)
```

## MQTT Architecture

- **Broker**: HiveMQ Cloud (TLS + user/pass auth). Backend → native MQTT-over-TLS `:8883` (`Mqtt:Transport=tcp`, `UseTls=true`, cred `acs-server`). ESP32 → wss `:8884/mqtt` (cred `esp32-acs`). Mosquitto/Docker `1883` still usable for local dev (`Mqtt:Transport=tcp`, `UseTls=false`).
- **ESP32 lib**: `esp_mqtt_client` (ESP-IDF native) — wss/TLS (cert bundle) + ALPN + auth, runs own task + auto-reconnect. PubSubClient dropped (no WebSocket support).
- **Backend lib**: MQTTnet (BackgroundService) — `WithWebSocketServer` + `WithTlsOptions` + credentials, config-driven (`Mqtt:Transport` = `websocket`|`tcp`, `Mqtt:WebSocketUri`, `Mqtt:UseTls`).
- **Device ID**: NOT hardcoded — firmware derives `esp32-door-<mac6>` from chip MAC at runtime; topics built at runtime. Backend auto-registers on first scan/heartbeat.
- **Topic prefix**: dropped (was `iot7f3a` for shared public broker). Private HiveMQ cluster needs none. `Mqtt:TopicPrefix` optional — set both sides only if re-sharing a broker.

### Topic Map
```
access/{deviceId}/scan      # ESP32 publish — UID payload
access/{deviceId}/response  # Backend publish — grant/deny + name
access/{deviceId}/status    # ESP32 heartbeat
devices/{deviceId}/command  # Backend → ESP32 commands (unlock, reboot)
```

### Payload Schema
```json
// ESP32 → broker (scan) — device = esp32-door-<mac6>, runtime-derived
{"device": "esp32-door-a1b2c3", "uid": "A1B2C3D4"}

// Backend → ESP32 (response)
{"access": true, "name": "Nguyen Van A"}
```

## Key Constraints

- RC522 runs 3.3V — never connect 5V pin, module dies
- CORS must include `http://localhost:5173` in dev, update `Program.cs` for prod
- EF migrations run before first API call — `dotnet ef database update`
- ESP32 card list hardcoded in firmware — update + reflash to add/remove users
- MQTT broker reachable before backend — MqttService retries connect on startup (5s loop)
- `esp_mqtt_client` runs own task — no `mqtt.loop()` needed; firmware polls `g_mqttConnected`/`responseReceived` flags
- Broker URI/auth: firmware `#define MQTT_WS_URI/USER/PASS` (top of `.ino`); backend `Mqtt:*` in `appsettings.json`. Keep `TopicPrefix` identical both sides

## Known Weak Points

- No auth on API endpoints — anyone on network hit `/api/users`
- MQTT broker now auth'd (user/pass over TLS wss). Creds in plaintext config/firmware — fine for project, rotate before real prod
- RFID UID spoofable — physical security only, not cryptographic
- SD card log + Postgres log desync if Wi-Fi drop mid-write
- MQTT QoS 0 currently — ESP32 SPIFFS queue buffers on disconnect/timeout, flush on reconnect (mitigates loss)
- MQTT message lost only if SPIFFS queue full (QUEUE_MAX=50, oldest dropped)

## Priorities When Resuming

1. ~~Swap HTTP POST → MQTT publish on ESP32 (PubSubClient)~~ DONE — AccessControl.ino
2. Add MqttService BackgroundService in .NET — subscribe `access/+/scan`
3. Add JWT auth to backend REST endpoints
4. Broker auth — mosquitto.conf username/password
5. Dashboard: SignalR push from MqttService (drop polling)
6. Admin UI: add/remove users without reflashing firmware
7. ~~ESP32 retry queue — store failed publishes in SPIFFS, flush on reconnect~~ DONE

## File Locations — Quick Reference

| What | Where |
|---|---|
| API endpoints | `backend/IoTAccessAPI/Program.cs` |
| DB models | `backend/IoTAccessAPI/Models/` |
| EF migrations | `backend/IoTAccessAPI/Migrations/` |
| MQTT service | `backend/IoTAccessAPI/Services/MqttService.cs` |
| Frontend routes | `frontend/app/src/routes/` |
| CORS config | `backend/IoTAccessAPI/Program.cs` |
| ESP32 firmware (Arduino IDE) | `esp32_logic/AccessControl.ino` |
| Local reference sketch | `esp32_logic/RFID.ino` (hardcoded-UID local-only demo) |
| Firmware config | top of `AccessControl.ino` (WiFi/MQTT/pins #define) |
| User validation | backend (MQTT response) — not hardcoded in firmware |

## Dev Gotchas

- Run postgres → broker → backend → frontend in order (Docker compose handles via healthchecks)
- Run backend before frontend — frontend fetches on mount, fails silent if API down
- `dotnet run` uses `https://localhost:7114`, not `http` — check `VITE_API_URL` in frontend `.env`
- TanStack Router gen route types on save — don't hand-edit `routeTree.gen.ts`. New routes need a dev-server run to regen before `tsc` build passes
- EF Core uses Npgsql (Postgres). Migrations are Postgres-native — `dotnet ef migrations add` needs Npgsql restored
- Only .NET 10 runtime on this box but project targets net8.0 — `dotnet ef` fails unless `DOTNET_ROLL_FORWARD=LatestMajor` is set for the command
- Two log tables: `AccessLog` (RFID scans, written by MqttService scan handler) + `EventLog` (door/connectivity/emergency). Door/emergency events written inline; connectivity online/offline logged by `DeviceMonitorService` (polls heartbeat every 60s, logs on transition only). All broadcast over SignalR (`NewAccessLog`/`NewEventLog`/`DoorStateChanged`)
- All API endpoints JWT-gated except `/api/auth/login` + `/api/health`. UI needs seeded Admin (SEED_DATA=true) to first login
- Debug MQTT with **MQTT Explorer** (GUI) — connect to broker, watch all topics live

## ESP32 Pin Map

Verified against `RFID.ino` real wiring. RC522 = SPI, OLED = I2C (separate buses).
RC522 SS on GPIO 5; OLED on ESP32 default I2C 21/22 — no pin shared.

| Module | Pin | Notes |
|---|---|---|
| RC522 SS (SDA) | GPIO 5 | SPI chip-select |
| RC522 SCK | GPIO 18 | SPI default |
| RC522 MOSI | GPIO 23 | SPI default |
| RC522 MISO | GPIO 19 | SPI default |
| RC522 RST | (driver-managed) | MFRC522v2 PinSimple |
| RC522 VCC | 3.3V only | 5V kills module |
| OLED SDA | GPIO 21 | I2C default |
| OLED SCL | GPIO 22 | I2C default |
| Buzzer | GPIO 32 | active buzzer, digitalWrite HIGH=on |

**Libs**: RFID = `MFRC522v2` (driver-based, NOT miguelbalboa v1). OLED = **SH1106**
(`Adafruit_SH110X`), NOT SSD1306. Buzzer active type — `digitalWrite`, not `tone()`.

## Project Overview
IoT Access Control System. Modern access control platform built with:
- **Backend**: .NET 8 Web API + Entity Framework Core
- **Frontend**: React + TanStack Router, Tailwind CSS, shadcn components
- **ESP32 Logic**: IoT device firmware for access control

## Directory Structure
```
.
├── backend/              # .NET Web API
│   └── IoTAccessAPI/     # Main API project
├── frontend/             # React application
│   └── app/              # Vite + React project
├── esp32_logic/          # ESP32 firmware
├── SETUP.md              # Setup and run instructions
└── CLAUDE.md             # This file
```

## Architecture

### Backend (IoTAccessAPI)
- **Framework**: ASP.NET Core 8
- **Database**: Entity Framework Core + PostgreSQL (Npgsql)
- **Key Features**:
  - RESTful API endpoints
  - CORS enabled for dev
  - Health check endpoint
  - Device management
  - Access logging
  - User management

**Default Port**: https://localhost:7114

### Frontend (app)
- **Framework**: React 19 + TypeScript
- **Routing**: TanStack Router (file-based)
- **Styling**: Tailwind CSS + shadcn components
- **Build Tool**: Vite
- **Dev Server Port**: http://localhost:5173

### ESP32 Logic
IoT firmware for physical access. Talks to broker via MQTT.

## Key Files & Endpoints

### Backend Endpoints
```
GET  /api/health         - Health check
GET  /api/devices        - List all devices
GET  /api/users          - List all users
GET  /api/access-logs    - List access logs (RFID scans)
GET  /api/event-logs     - List device events (door/connectivity/emergency, Admin)
GET  /swagger            - API documentation
```

### Frontend Routes (JWT-gated, control-room UI)
```
/login           - Operator login (JWT)
/                - Overview: stats + live SignalR access feed
/logs            - Access log archive (filter grant/deny)
/devices         - Device registry + online/offline status
/cards           - RFID credential registry (enroll/revoke)
/users           - Operator identities (CRUD)
```

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+
- PostgreSQL 16 (or use Docker compose)
- Docker (Mosquitto broker + Postgres)

### Quick Start (Docker — full stack)
```bash
cp .env.example .env       # edit secrets
docker compose up --build  # postgres + mosquitto + backend + frontend
# → http://localhost  (login admin / admin123, change before prod)
```

### Quick Start (local dev, no Docker)
```bash
# Terminal 1: Postgres + Broker
docker run -d --name pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=IoTAccessDb -p 5432:5432 postgres:16-alpine
docker run -d --name mosquitto -p 1883:1883 eclipse-mosquitto

# Terminal 2: Backend (auto-migrate + seed in Development)
cd backend/IoTAccessAPI && dotnet restore && dotnet run

# Terminal 3: Frontend
cd frontend/app && npm install && npm run dev
```

### Environment Variables

**Backend** (appsettings.Development.json or env):
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=IoTAccessDb;Username=postgres;Password=postgres
Mqtt__Host=localhost
Mqtt__Port=1883
SEED_DATA=true                 # seed users + device on startup (idempotent)
```

**Compose** (.env — see .env.example): POSTGRES_*, JWT_SECRET, ADMIN_USERNAME/PASSWORD, MQTT_*

**Frontend** (.env):
```
VITE_API_URL=https://localhost:7114
```

## Development Workflow

### Adding a New Route (Frontend)
1. Create file in `frontend/app/src/routes/`
2. Use TanStack Router file-based routing
3. Import TanStack Router hooks

### Adding an API Endpoint (Backend)
1. Create controller or minimal API
2. Add endpoint in `Program.cs`
3. Configure CORS
4. Document in Swagger

### Running Tests
```bash
# Backend
cd backend/IoTAccessAPI
dotnet test

# Frontend
cd frontend/app
npm run lint
```

## Useful Commands

### Backend
```bash
dotnet run                    # Run dev server
dotnet build                  # Build project
dotnet test                   # Run tests
dotnet ef migrations add NAME # Add EF Core migration
dotnet ef database update     # Apply migrations
```

### Frontend
```bash
npm run dev                   # Start dev server
npm run build                 # Build for production
npm run preview               # Preview production build
npm run lint                  # Run ESLint
```

### MQTT Debug
```bash
# Subscribe all topics (requires mosquitto-clients)
mosquitto_sub -h localhost -t "access/#" -v

# Simulate ESP32 scan
mosquitto_pub -h localhost -t "access/esp32-door-01/scan" \
  -m '{"device":"esp32-door-01","uid":"A1B2C3D4"}'
```

## Git Workflow
- Branch naming: `feature/`, `bugfix/`, `chore/`
- Commit messages: descriptive, present tense
- `.gitignore` covers .NET + Node.js projects

## CORS Configuration
Dev CORS enabled for:
- `http://localhost:5173` (Vite dev)
- `http://localhost:3000` (alt port)

Update `backend/IoTAccessAPI/Program.cs` for prod.

## Deployment Notes
- Build frontend: `npm run build`
- Publish backend: `dotnet publish -c Release`
- Configure CORS for prod
- Setup DB migrations for prod
- Run Mosquitto as service or managed cloud broker

## Troubleshooting

### CORS Errors
Check `Program.cs` CORS policy includes frontend URL

### Database Connection
Verify connection string in `appsettings.Development.json`

### Module Not Found (Frontend)
Run `npm install` in `frontend/app/`

### Port Already in Use
- Change frontend port in `vite.config.ts`
- Change backend port in `launchSettings.json`

### MQTT Not Connecting
- Broker running? `docker ps`
- Port 1883 open? `telnet localhost 1883`
- Check MqttService logs in backend console