using IoTAccessAPI.Data;
using IoTAccessAPI.DTOs.AccessLogs;
using IoTAccessAPI.DTOs.Cards;
using IoTAccessAPI.DTOs.Users;
using IoTAccessAPI.Models;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync() =>
        await _db.Users
            .Select(u => ToDto(u))
            .ToListAsync();

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> CreateAsync(CreateUserRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return null;

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role ?? "User"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Optionally register an RFID card at the same time
        if (!string.IsNullOrWhiteSpace(request.RfidUid))
        {
            _db.RfidCards.Add(new RfidCard { Uid = request.RfidUid, UserId = user.Id });
            await _db.SaveChangesAsync();
        }

        return ToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return null;

        if (request.Username is not null) user.Username = request.Username;
        if (request.Role is not null) user.Role = request.Role;

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;

        user.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccessLogDto>> GetAccessLogsAsync(int userId) =>
        await _db.AccessLogs
            .Include(a => a.Device)
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AccessLogDto(
                a.Id, a.RequestId, a.RfidUid, a.AccessGranted, a.DenyReason,
                a.Timestamp, a.DeviceId, a.Device.Name, a.UserId, a.User!.Username))
            .ToListAsync();

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Username, u.Role, u.IsActive, u.CreatedAt);
}
