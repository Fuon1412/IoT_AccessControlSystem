using IoTAccessAPI.DTOs.Auth;
using IoTAccessAPI.DTOs.Users;

namespace IoTAccessAPI.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<UserDto?> RegisterAsync(RegisterRequest request);
}
