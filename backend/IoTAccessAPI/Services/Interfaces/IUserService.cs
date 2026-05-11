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
    Task<IEnumerable<AccessLogDto>> GetAccessLogsAsync(int userId);
}
