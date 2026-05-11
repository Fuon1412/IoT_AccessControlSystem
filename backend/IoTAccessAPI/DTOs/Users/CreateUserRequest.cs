using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.Users;

public record CreateUserRequest(
    [Required] string Username,
    [Required] string Password,
    string? Role,
    string? RfidUid);   // optional: register a card at same time
