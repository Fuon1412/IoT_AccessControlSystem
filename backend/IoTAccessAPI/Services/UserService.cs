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
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Username : request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role ?? "Employee"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Optionally assign an RFID card at the same time.
        // If the UID was already auto-stored (unassigned), reuse that row.
        if (!string.IsNullOrWhiteSpace(request.RfidUid))
        {
            var uid = request.RfidUid.Trim().ToLowerInvariant();
            var card = await _db.RfidCards.FirstOrDefaultAsync(c => c.Uid == uid);
            if (card is null)
            {
                _db.RfidCards.Add(new RfidCard
                {
                    Uid = uid, UserId = user.Id, IsAssigned = true, IsActive = true,
                });
            }
            else
            {
                card.UserId = user.Id;
                card.IsAssigned = true;
                card.IsActive = true;
            }
            await _db.SaveChangesAsync();
        }

        return ToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return null;

        // reject duplicate username on rename
        if (request.Username is not null && request.Username != user.Username)
        {
            if (await _db.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
                return null;
            user.Username = request.Username;
        }
        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.Role is not null) user.Role = request.Role;
        if (request.IsActive is not null) user.IsActive = request.IsActive.Value;

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

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;

        // Hard delete. AccessLog.UserId + RfidCard.UserId are SetNull on delete,
        // so logs survive (orphaned) and any card becomes unassigned.
        var cards = await _db.RfidCards.Where(c => c.UserId == id).ToListAsync();
        foreach (var card in cards) { card.UserId = null; card.IsAssigned = false; }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;

        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int id, string newPassword)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int id, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
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
        new(u.Id, u.Username, u.FullName, u.Role, u.IsActive, u.CreatedAt);
}
