# IoT Access Control System

Physical access control platform. ESP32 scans RFID cards → validates against backend → grants/denies entry → dashboard updates in real time.

---

## Stack

| Layer | Tech |
|---|---|
| Backend | .NET 8 Web API, EF Core, PostgreSQL (Npgsql) |
| Frontend | React 19, TanStack Router, Tailwind CSS, shadcn |
| IoT Device | ESP32, RC522 RFID, OLED, SD card, buzzer |
| Real-time | SignalR WebSocket |
| Auth | JWT Bearer, BCrypt, role-based |

---

## Architecture

```
RC522 (RFID scan)
  → ESP32 publishes MQTT: access/{deviceId}/scan  {device, uid}
  → MqttService (BackgroundService) validates UID + writes log
  → EF Core → PostgreSQL
  → backend publishes  access/{deviceId}/response  {access, name}
  → ESP32: GRANTED shows name on OLED / DENIED buzzes
  → SignalR broadcasts NewAccessLog to dashboard
  → React dashboard updates live
Offline → ESP32 buffers scan in SPIFFS, flushes on reconnect
```

> REST endpoints (`/api/cards/validate`, `POST /api/access-logs`) remain available
> for tooling/manual use, but live device flow goes through MQTT.

### Backend Structure

```
IoTAccessAPI/
├── Controllers/         # HTTP layer only — no business logic
│   ├── AuthController        POST /api/auth/login, /register
│   ├── UsersController       CRUD /api/users + /{id}/access-logs
│   ├── DevicesController     CRUD /api/devices + /status + /heartbeat
│   ├── AccessLogsController  GET + POST /api/access-logs
│   ├── CardsController       CRUD /api/cards + /validate/{uid}
│   └── HealthController      GET /api/health + /health/devices
├── Services/
│   ├── Interfaces/      # IAuthService, IUserService, IDeviceService,
│   │                    # IAccessLogService, IRfidCardService
│   └── *.cs             # Implementations — all business logic lives here
├── DTOs/                # Request/response records (Auth, Users, Devices, Cards, AccessLogs)
├── Models/              # EF entities: User, Device, AccessLog, RfidCard
├── Data/AppDbContext.cs
├── Hubs/AccessHub.cs    # SignalR hub at /hubs/access
└── Program.cs           # DI wiring + middleware only
```

### Roles

| Role | Access |
|---|---|
| `Admin` | Full CRUD — users, devices, cards, logs |
| `User` | Read-only — dashboard, devices, logs |
| `Device` | POST access-logs + PATCH heartbeat + GET cards/validate |

ESP32 uses a `Device`-role JWT service account.

---

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+
- PostgreSQL 16 (or use Docker compose — below)

### Docker (full stack — recommended)

```bash
cp .env.example .env          # edit secrets
docker compose up --build     # postgres + mosquitto + backend + frontend
```
UI at `http://localhost`. First boot seeds Admin (`admin` / `admin123` — change before prod).

### Backend (local dev)

```bash
# Postgres (or point connection string at an existing instance)
docker run -d --name pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=IoTAccessDb -p 5432:5432 postgres:16-alpine

cd backend/IoTAccessAPI
dotnet restore
dotnet run                    # auto-migrates + seeds in Development
```

API: `https://localhost:7114` | Swagger: `https://localhost:7114/swagger`

### Frontend

```bash
cd frontend/app
npm install
npm run dev
```

UI: `http://localhost:5173`

### Environment

**`backend/IoTAccessAPI/appsettings.Development.json`** — already scaffolded:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=IoTAccessDb;Username=postgres;Password=postgres"
  },
  "Jwt": { "Secret": "dev-secret-change-in-production-min-32-chars!!" },
  "Seed": { "AdminUsername": "admin", "AdminPassword": "admin123" },
  "SEED_DATA": true
}
```

**`frontend/app/.env`**:
```
VITE_API_URL=https://localhost:7114
```

### Seed data

`SEED_DATA=true` runs an idempotent startup seed (`Data/DbSeeder.cs`):
operators (`admin`/`operator`/`door-service`) + device `esp32-door-01` (matches firmware `DEVICE_ID`).
RFID cards are **not** seeded — assign UID → user via the Cards screen or `POST /api/cards`.

---

## API Reference

### Auth
```
POST /api/auth/login          { username, password } → { token, expires, role }
POST /api/auth/register       [Admin] { username, password, role? }
```

### Users
```
GET    /api/users             [Admin] list all
POST   /api/users             [Admin] { username, password, role?, rfidUid? }
GET    /api/users/{id}        [Admin]
PUT    /api/users/{id}        [Admin] { username?, role? }
DELETE /api/users/{id}        [Admin] soft-delete
GET    /api/users/{id}/access-logs  [Admin]
```

### Devices
```
GET    /api/devices           [Admin,User]
POST   /api/devices           [Admin] { name, macAddress, location }
GET    /api/devices/{id}      [Admin,User]
PUT    /api/devices/{id}      [Admin] { name?, location? }
DELETE /api/devices/{id}      [Admin] decommission
GET    /api/devices/{id}/status     [Admin,User] online/offline + last heartbeat
PATCH  /api/devices/{id}/heartbeat  [Admin,Device]
```

### RFID Cards
```
GET    /api/cards             [Admin]
POST   /api/cards             [Admin] { uid, userId }
DELETE /api/cards/{id}        [Admin] deactivate
GET    /api/cards/validate/{uid}    [Admin,Device] → { isValid, userId, username, role }
```

### Access Logs
```
GET  /api/access-logs         [Admin,User]
POST /api/access-logs         [Admin,Device] { requestId, rfidUid, deviceId, accessGranted, denyReason?, timestamp? }
```
`requestId` is a GUID generated by ESP32. Duplicate posts return the existing log (idempotent — safe to retry).

### Health
```
GET /api/health               public — { status, database, timestamp }
GET /api/health/devices       public — per-device heartbeat + silent alert
```

---

## ESP32 Integration

### Pin Map

Verified against `esp32_logic/RFID.ino`. RC522 = SPI, OLED = I2C (separate buses).
Libs: **MFRC522v2** (driver-based), **SH1106** OLED (`Adafruit_SH110X`), active buzzer (`digitalWrite`).

| Module | GPIO |
|---|---|
| RC522 SS (SDA) | 5 |
| RC522 SCK | 18 |
| RC522 MOSI | 23 |
| RC522 MISO | 19 |
| OLED SDA | 21 (I2C) |
| OLED SCL | 22 (I2C) |
| Buzzer | 32 (active) |
| RC522 VCC | **3.3V only — never 5V** |

### Flow

1. Scan card → read UID (lowercase hex, no separator)
2. Publish `access/{deviceId}/scan` `{device, uid}` to broker
3. MqttService validates + logs, publishes `access/{deviceId}/response` `{access, name}`
4. GRANTED → OLED shows user name + confirm beep · DENIED → "DENIED!" + 3 alarm beeps
5. On disconnect/timeout → buffer scan in SPIFFS, flush on reconnect
6. Heartbeat: publish `access/{deviceId}/status` = "online" every 60s

---

## Known Limitations

| Issue | Impact | Mitigation |
|---|---|---|
| RFID UID spoofable | Physical cloning possible | Layer with PIN or NFC |
| JWT secret in appsettings | Leaked config = full compromise | Use env var / secrets manager in prod |
| No token refresh | 8h expiry, then re-login | Add refresh token endpoint (Phase 5) |
| SignalR no auth on hub | Any client can connect | Add `[Authorize]` to AccessHub |

---

## Development Notes

- Start backend before frontend — frontend fetches on mount, fails silently if API down
- `dotnet run` defaults to `https://localhost:7114` — match `VITE_API_URL`
- Don't hand-edit `routeTree.gen.ts` — TanStack Router regenerates on save (new routes need a dev-server run before `tsc` build passes)
- First run: `SEED_DATA=true` creates Admin (`admin`/`admin123`). Change password before prod
- `DeviceSilenceThresholdMinutes` in `appsettings.json` controls when a device is flagged silent (default: 5)
