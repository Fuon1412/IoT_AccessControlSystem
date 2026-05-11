# IoT Access Control System - Project Documentation

## Project Overview
IoT Access Control System is a modern access control management platform built with:
- **Backend**: .NET 8 Web API with Entity Framework Core
- **Frontend**: React with TanStack Router, Tailwind CSS, and shadcn components
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
- **Database**: Entity Framework Core with SQL Server
- **Key Features**:
  - RESTful API endpoints
  - CORS enabled for development
  - Health check endpoint
  - Device management
  - Access logging
  - User management

**Default Port**: https://localhost:7114

### Frontend (app)
- **Framework**: React 19 with TypeScript
- **Routing**: TanStack Router (file-based)
- **Styling**: Tailwind CSS with shadcn components
- **Build Tool**: Vite
- **Dev Server Port**: http://localhost:5173

### ESP32 Logic
- IoT device firmware for physical access control
- Communication with backend API

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
- SQL Server (or configure alternative database)

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
3. Import TanStack Router hooks as needed

### Adding an API Endpoint (Backend)
1. Create controller or use minimal API approach
2. Add endpoint in `Program.cs`
3. Configure CORS if needed
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
- `.gitignore` covers both .NET and Node.js projects

## CORS Configuration
Development CORS is enabled for:
- `http://localhost:5173` (Vite dev)
- `http://localhost:3000` (alternative port)

Update in `backend/IoTAccessAPI/Program.cs` for production.

## Deployment Notes
- Build frontend: `npm run build`
- Publish backend: `dotnet publish -c Release`
- Configure production CORS accordingly
- Set up database migrations for production

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
