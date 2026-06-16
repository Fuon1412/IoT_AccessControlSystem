using IoTAccessAPI.DTOs.Cards;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTAccessAPI.Controllers;

[ApiController]
[Route("api/cards")]
public class CardsController : ControllerBase
{
    private readonly IRfidCardService _cardService;

    public CardsController(IRfidCardService cardService)
    {
        _cardService = cardService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var cards = await _cardService.GetAllAsync();
        return Ok(cards);
    }

    /// <summary>Cards seen by readers but not yet assigned to a user.</summary>
    [HttpGet("unassigned")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUnassigned()
    {
        var cards = await _cardService.GetUnassignedAsync();
        return Ok(cards);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterCardRequest request)
    {
        var result = await _cardService.RegisterAsync(request);
        if (result is null)
            return Conflict(new { error = "User not found" });

        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>Assign an existing (e.g. auto-stored unassigned) card to a user.</summary>
    [HttpPut("{id:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignCardRequest request)
    {
        var result = await _cardService.AssignAsync(id, request.UserId);
        return result is null ? NotFound(new { error = "Card or user not found" }) : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var success = await _cardService.DeactivateAsync(id);
        return success ? NoContent() : NotFound();
    }

    /// <summary>ESP32 calls this to validate a scanned UID. Requires Device role JWT.</summary>
    [HttpGet("validate/{uid}")]
    [Authorize(Roles = "Admin,Device")]
    public async Task<IActionResult> Validate(string uid)
    {
        var result = await _cardService.ValidateAsync(uid);
        return Ok(result);
    }
}
