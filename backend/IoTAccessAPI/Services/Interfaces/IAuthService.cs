using IoTAccessAPI.DTOs.Auth;
using IoTAccessAPI.DTOs.Users;

namespace IoTAccessAPI.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<UserDto?> RegisterAsync(RegisterRequest request);

    /// <summary>Verify a username/password pair (for re-confirming sensitive actions).</summary>
    Task<bool> VerifyPasswordAsync(string username, string password);
}
