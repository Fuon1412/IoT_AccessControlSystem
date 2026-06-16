using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.Users;

/// <summary>Self-service password change — verifies current password.</summary>
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(6)] string NewPassword);

/// <summary>Admin reset — sets a new password without the old one.</summary>
public record ResetPasswordRequest([Required, MinLength(6)] string NewPassword);
