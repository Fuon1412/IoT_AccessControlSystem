# Backend API - CLAUDE.md

## Project Info
- **Type**: ASP.NET Core 8 Web API
- **Language**: C#
- **Database**: Entity Framework Core + SQL Server
- **Port**: https://localhost:7114

## Quick Commands
```bash
dotnet run              # Run dev server
dotnet build            # Build project
dotnet test             # Run tests
dotnet ef migrations add [Name]  # Create migration
dotnet ef database update        # Apply migrations
```

## Key Files
- `Program.cs` - App configuration, endpoints, CORS
- `appsettings.json` - Configuration
- `appsettings.Development.json` - Dev configuration
- `IoTAccessAPI.csproj` - Project file with dependencies

## CORS Configuration
Located in `Program.cs`:
- Development URLs: `localhost:5173`, `localhost:3000`
- Update for production environments

## API Endpoints
```
GET  /api/health         - Health check
GET  /api/devices        - List devices
GET  /api/users          - List users
GET  /api/access-logs    - List access logs
GET  /swagger            - Swagger UI
```

## Development Tips
1. Use Swagger UI for testing endpoints: https://localhost:7114/swagger
2. Check launchSettings.json for port configuration
3. Database migrations: `dotnet ef migrations add Migration_Name`
4. Connection string in appsettings.Development.json

## Common Issues
- **Port already in use**: Check launchSettings.json
- **CORS errors**: Verify frontend URL in Program.cs CORS policy
- **Database connection**: Check connection string in appsettings.Development.json
- **SSL certificate errors**: Ensure you're using https://localhost:7114
