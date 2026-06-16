using System.ComponentModel.DataAnnotations;

namespace IoTAccessAPI.DTOs.Cards;

public record AssignCardRequest([Required] int UserId);
