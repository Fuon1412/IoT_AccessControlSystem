# Backend API — Implementation Plan

Derived from project CLAUDE.md insights. Backend-scoped priorities only.

## Phase 1: Security (Critical)

**Goal**: Lock down exposed API endpoints.

### 1.1 JWT Authentication
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- Configure JWT in `Program.cs` (issuer, audience, secret from config)
- Add `[Authorize]` to all endpoints except `/api/health` and `/swagger`
- Create `POST /api/auth/login` endpoint (username + password → JWT)
- Create `POST /api/auth/register` endpoint (admin-only)
- Store hashed passwords (BCrypt) in User model
- Add EF migration for auth fields (PasswordHash, Role)

### 1.2 Role-Based Access
- Roles: `Admin`, `User`, `Device`
- Admin: full CRUD on users, devices, logs
- Device: POST access-logs only (ESP32 service account)
- User: read-only dashboard access

**Known risk**: No auth now — anyone on network hit `/api/users`. Fix first.

## Phase 2: Real-Time Communication

**Goal**: Replace polling with push updates.

### 2.1 SignalR Hub
- Add `Microsoft.AspNetCore.SignalR` package
- Create `AccessHub` — broadcast new access log entries
- Wire hub in `Program.cs` (`/hubs/access`)
- Update CORS to allow SignalR WebSocket connections
- On new access log POST → notify all connected dashboard clients

### 2.2 ESP32 Endpoint Hardening
- Validate POST body schema on `/api/access-logs`
- Return proper error codes (400 bad request, 401 unauthorized)
- Log failed attempts separately (audit trail)

## Phase 3: Admin API

**Goal**: Manage users/devices without firmware reflash.

### 3.1 User CRUD
- `POST /api/users` — add user + RFID UID mapping
- `PUT /api/users/{id}` — update user
- `DELETE /api/users/{id}` — soft delete (keep audit trail)
- `GET /api/users/{id}/access-logs` — per-user log history

### 3.2 Device CRUD
- `POST /api/devices` — register new ESP32
- `PUT /api/devices/{id}` — update config
- `DELETE /api/devices/{id}` — decommission
- `GET /api/devices/{id}/status` — last heartbeat, online/offline

### 3.3 RFID Card Registry
- New model: `RfidCard` (UID, UserId, Active, RegisteredAt)
- `GET /api/cards` — list all registered cards
- `POST /api/cards` — register card to user
- `DELETE /api/cards/{id}` — deactivate card
- ESP32 queries `/api/cards/validate/{uid}` instead of hardcoded map

## Phase 4: Data Integrity

**Goal**: Fix SD card / SQL Server desync.

### 4.1 Idempotent Access Logging
- Add `RequestId` (GUID) to access log POST body
- ESP32 generates ID locally, sends with request
- Backend deduplicates on RequestId (upsert, not insert)
- Supports ESP32 retry queue (SPIFFS buffer → flush on reconnect)

### 4.2 Health Monitoring
- Expand `/api/health` — include DB connection status, last ESP32 heartbeat
- Add `/api/health/devices` — per-device last-seen timestamp
- Alert if device silent >5 min (configurable threshold)

## File Impact Map

| File | Changes |
|---|---|
| `Program.cs` | JWT config, SignalR hub, new endpoints, CORS update |
| `Models/` | User (add auth fields), RfidCard (new), AccessLog (add RequestId) |
| `Migrations/` | New migrations per phase |
| `appsettings.json` | JWT secret, SignalR config |
| `.csproj` | New packages (JWT, SignalR, BCrypt) |

## Order of Execution

```
Phase 1.1 → 1.2 → 2.1 → 2.2 → 3.3 → 3.1 → 3.2 → 4.1 → 4.2
```

Phase 1 first — no point building features on unsecured API.
Phase 3.3 (card registry) before 3.1 (user CRUD) — ESP32 needs card validation endpoint to drop hardcoded map.
