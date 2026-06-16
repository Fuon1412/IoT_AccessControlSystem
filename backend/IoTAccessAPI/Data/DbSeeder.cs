using IoTAccessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Data;

/// <summary>
/// Idempotent startup seed. Gated by SEED_DATA=true.
/// Seeds operators + a device matching firmware DEVICE_ID.
/// Cards are intentionally NOT seeded — assign UID → user later via UI/API.
/// Safe to run every boot: each entity is inserted only if absent.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (!config.GetValue<bool>("SEED_DATA"))
        {
            logger.LogInformation("[Seed] SEED_DATA not set — skipping");
            return;
        }

        var adminUser = config["Seed:AdminUsername"] ?? "admin";
        var adminPass = config["Seed:AdminPassword"] ?? "admin123";

        await SeedUsersAsync(db, logger, adminUser, adminPass);
        await SeedDevicesAsync(db, logger);

        logger.LogInformation("[Seed] complete");
    }

    private static async Task SeedUsersAsync(AppDbContext db, ILogger logger, string adminUser, string adminPass)
    {
        // (username, password, fullName, role)
        var wanted = new (string Username, string Password, string FullName, string Role)[]
        {
            (adminUser, adminPass, "Administrator", "Admin"),
            ("employee1", "employee123", "Nguyen Van A", "Employee"),
            ("door-service", "device123", "Door Service", "Device"),
        };

        foreach (var w in wanted)
        {
            if (await db.Users.AnyAsync(u => u.Username == w.Username))
            {
                logger.LogInformation("[Seed] user '{User}' exists — skip", w.Username);
                continue;
            }

            db.Users.Add(new User
            {
                Username = w.Username,
                FullName = w.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(w.Password),
                Role = w.Role,
                IsActive = true,
            });
            logger.LogInformation("[Seed] + user '{User}' ({Role})", w.Username, w.Role);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDevicesAsync(AppDbContext db, ILogger logger)
    {
        // Name MUST match firmware DEVICE_ID (AccessControl.ino) so MQTT scans resolve.
        var wanted = new (string Name, string Mac, string Location)[]
        {
            ("esp32-door-01", "AA:BB:CC:DD:EE:01", "Main Entrance"),
        };

        foreach (var w in wanted)
        {
            if (await db.Devices.AnyAsync(d => d.Name == w.Name || d.MacAddress == w.Mac))
            {
                logger.LogInformation("[Seed] device '{Dev}' exists — skip", w.Name);
                continue;
            }

            db.Devices.Add(new Device
            {
                Name = w.Name,
                MacAddress = w.Mac,
                Location = w.Location,
                IsActive = true,
            });
            logger.LogInformation("[Seed] + device '{Dev}'", w.Name);
        }

        await db.SaveChangesAsync();
    }
}
