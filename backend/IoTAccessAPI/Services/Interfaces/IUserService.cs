using IoTAccessAPI.DTOs.AccessLogs;
using IoTAccessAPI.DTOs.Users;

namespace IoTAccessAPI.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(int id);
    Task<UserDto?> CreateAsync(CreateUserRequest request);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request);
    Task<bool> DeactivateAsync(int id);
    Task<bool> DeleteAsync(int id);
    Task<bool> SetActiveAsync(int id, bool isActive);
    Task<IEnumerable<AccessLogDto>> GetAccessLogsAsync(int userId);

    /// <summary>Admin reset — overwrite password, no current-password check.</summary>
    Task<bool> ResetPasswordAsync(int id, string newPassword);
    /// <summary>Self-service — verify current password before changing.</summary>
    Task<bool> ChangePasswordAsync(int id, string currentPassword, string newPassword);
}
