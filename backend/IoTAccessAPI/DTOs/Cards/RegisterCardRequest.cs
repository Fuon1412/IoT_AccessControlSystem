using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.Cards;

public record RegisterCardRequest([Required, StringLength(50)] string Uid, [Required] int UserId);
