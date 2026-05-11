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
- **MQTT (planned)** — replacing HTTP POST, bidirectional, auto-reconnect, lower latency

## Data Flow

```
Current (REST):
ESP32 (RFID scan) → POST /api/access-logs → EF Core → SQL Server → Frontend polls

Planned (MQTT):
ESP32 scan → publish access/{deviceId}/scan → Mosquitto broker
Broker → MqttService (BackgroundService) → DB write + publish access/{deviceId}/response
ESP32 ← response (grant/deny) ← broker
Frontend ← SignalR push from MqttService (no polling)
```

## MQTT Architecture

- **Broker**: Mosquitto (Docker: port 1883)
- **ESP32 lib**: PubSubClient
- **Backend lib**: MQTTnet (BackgroundService)
- **Cloud alt**: HiveMQ Cloud / EMQX Cloud (no self-host)

### Topic Map
```
access/{deviceId}/scan      # ESP32 publish — UID payload
access/{deviceId}/response  # Backend publish — grant/deny + name
access/{deviceId}/status    # ESP32 heartbeat
devices/{deviceId}/command  # Backend → ESP32 commands (unlock, reboot)
```

### Payload Schema
```json
// ESP32 → broker (scan)
{"device": "esp32-door-01", "uid": "A1B2C3D4"}

// Backend → ESP32 (response)
{"access": true, "name": "Nguyen Van A"}
```

## Key Constraints

- RC522 runs 3.3V — never connect 5V pin, module dies
- CORS must include `http://localhost:5173` in dev, update `Program.cs` for prod
- EF migrations run before first API call — `dotnet ef database update`
- ESP32 card list hardcoded in firmware — update + reflash to add/remove users
- MQTT broker must start before backend — MqttService connect on startup
- `mqtt.loop()` must run every loop() iteration on ESP32 — blocks if omitted

## Known Weak Points

- No auth on API endpoints — anyone on network hit `/api/users`
- No auth on MQTT broker — add username/password in mosquitto.conf for prod
- RFID UID spoofable — physical security only, not cryptographic
- SD card log + SQL Server log desync if Wi-Fi drop mid-write
- ESP32 HTTP POST: no retry (current) — MQTT QoS 1 fixes this when implemented
- MQTT message lost if broker down + ESP32 not buffering locally

## Priorities When Resuming

1. Swap HTTP POST → MQTT publish on ESP32 (PubSubClient)
2. Add MqttService BackgroundService in .NET — subscribe `access/+/scan`
3. Add JWT auth to backend REST endpoints
4. Broker auth — mosquitto.conf username/password
5. Dashboard: SignalR push from MqttService (drop polling)
6. Admin UI: add/remove users without reflashing firmware
7. ESP32 retry queue — store failed publishes in SPIFFS, flush on reconnect

## File Locations — Quick Reference

| What | Where |
|---|---|
| API endpoints | `backend/IoTAccessAPI/Program.cs` |
| DB models | `backend/IoTAccessAPI/Models/` |
| EF migrations | `backend/IoTAccessAPI/Migrations/` |
| MQTT service | `backend/IoTAccessAPI/Services/MqttService.cs` |
| Frontend routes | `frontend/app/src/routes/` |
| CORS config | `backend/IoTAccessAPI/Program.cs` |
| ESP32 firmware | `esp32_logic/` |
| RFID card list | `esp32_logic/main.cpp` (hardcoded map) |

## Dev Gotchas

- Run broker → backend → frontend in order
- Run backend before frontend — frontend fetches on mount, fails silent if API down
- `dotnet run` uses `https://localhost:7114`, not `http` — check `VITE_API_URL` in `.env`
- TanStack Router gen route types on save — don't hand-edit `routeTree.gen.ts`
- EF Core needs SQL Server by default — swap SQLite for local dev without SQL Server install
- Debug MQTT with **MQTT Explorer** (GUI) — connect to broker, watch all topics live

## ESP32 Pin Map

| Module | Pin |
|---|---|
| RC522 SDA | GPIO 21 |
| RC522 SCK | GPIO 18 |
| RC522 MOSI | GPIO 23 |
| RC522 MISO | GPIO 19 |
| RC522 RST | GPIO 22 |
| OLED SDA | GPIO 21 (shared I2C) |
| OLED SCL | GPIO 22 (shared I2C) |
| SD card CS | GPIO 5 |
| Buzzer | GPIO 4 |
| RC522 VCC | 3.3V only |

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
- **Database**: Entity Framework Core + SQL Server
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
GET  /api/access-logs    - List access logs
GET  /swagger            - API documentation
```

### Frontend Routes
```
/                - Home page
/dashboard       - Dashboard with system overview
```

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+
- SQL Server (or configure alternative DB)
- Docker (for Mosquitto broker)

### Quick Start
```bash
# Terminal 1: Broker
docker run -d --name mosquitto -p 1883:1883 eclipse-mosquitto

# Terminal 2: Backend
cd backend/IoTAccessAPI
dotnet restore
dotnet run

# Terminal 3: Frontend
cd frontend/app
npm install
npm run dev
```

### Environment Variables

**Backend** (.env or appsettings.Development.json):
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionString=Server=.;Database=IoTAccessDb;Integrated Security=True;
MQTT_HOST=localhost
MQTT_PORT=1883
```

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