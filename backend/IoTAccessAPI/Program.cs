var builder = WebApplication.CreateBuilder(args);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

// Use CORS
app.UseCors("DevelopmentPolicy");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithOpenApi();

// Device endpoints
app.MapGet("/api/devices", () => new { devices = new List<object>(), total = 0 })
.WithName("GetDevices")
.WithOpenApi();

// Access log endpoints
app.MapGet("/api/access-logs", () => new { logs = new List<object>(), total = 0 })
.WithName("GetAccessLogs")
.WithOpenApi();

// User endpoints
app.MapGet("/api/users", () => new { users = new List<object>(), total = 0 })
.WithName("GetUsers")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
