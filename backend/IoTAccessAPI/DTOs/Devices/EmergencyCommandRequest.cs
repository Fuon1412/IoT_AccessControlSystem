using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.Devices;

/// <summary>
/// Emergency door command. Admin must re-confirm password.
/// Action: "lock" → force-lock, "unlock" → force-open.
/// </summary>
public record EmergencyCommandRequest(
    [Required] string Action,
    [Required] string Password);
