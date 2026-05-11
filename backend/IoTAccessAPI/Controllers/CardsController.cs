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
        return Ok(new { cards, total = cards.Count() });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterCardRequest request)
    {
        var result = await _cardService.RegisterAsync(request);
        if (result is null)
            return Conflict(new { error = "UID already registered or user not found" });

        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
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
