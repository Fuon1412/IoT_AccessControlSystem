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

## Data Flow

```
ESP32 (RFID scan)
  → POST /api/access-logs  (Wi-Fi, JSON)
  → EF Core writes to SQL Server
  → Frontend polls /api/access-logs
  → Dashboard updates
```

## Key Constraints

- RC522 runs 3.3V — never connect 5V pin, module dies
- CORS must include `http://localhost:5173` in dev, update `Program.cs` for prod
- EF migrations run before first API call — `dotnet ef database update`
- ESP32 MAC list hardcoded in firmware — update + reflash to add/remove users

## Known Weak Points

- No auth on API endpoints — anyone on network hit `/api/users`
- RFID UID spoofable — physical security only, not cryptographic
- SD card log + SQL Server log desync if Wi-Fi drop mid-write
- No retry logic on ESP32 HTTP POST — failed requests silently dropped

## Priorities When Resuming

1. Add JWT auth to backend endpoints
2. Add retry queue on ESP32 for failed POSTs (store SPIFFS, flush on reconnect)
3. Dashboard: real-time via SignalR instead of polling
4. Admin UI: add/remove users without reflashing firmware

## File Locations — Quick Reference

| What | Where |
|---|---|
| API endpoints | `backend/IoTAccessAPI/Program.cs` |
| DB models | `backend/IoTAccessAPI/Models/` |
| EF migrations | `backend/IoTAccessAPI/Migrations/` |
| Frontend routes | `frontend/app/src/routes/` |
| CORS config | `backend/IoTAccessAPI/Program.cs` |
| ESP32 firmware | `esp32_logic/` |
| RFID card list | `esp32_logic/main.cpp` (hardcoded map) |

## Dev Gotchas

- Run backend before frontend — frontend fetches on mount, fails silent if API down
- `dotnet run` uses `https://localhost:7114`, not `http` — check `VITE_API_URL` in `.env`
- TanStack Router gen route types on save — don't hand-edit `routeTree.gen.ts`
- EF Core needs SQL Server by default — swap SQLite for local dev without SQL Server install

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
IoT firmware for physical access. Talks to backend API.

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

### Quick Start
```bash
# Terminal 1: Backend
cd backend/IoTAccessAPI
dotnet restore
dotnet run

# Terminal 2: Frontend
cd frontend/app
npm install
npm run dev
```

### Environment Variables

**Backend** (.env or appsettings.Development.json):
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionString=Server=.;Database=IoTAccessDb;Integrated Security=True;
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