using IoTAccessAPI.Data;
using IoTAccessAPI.DTOs.Cards;
using IoTAccessAPI.Models;
using IoTAccessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IoTAccessAPI.Services;

public class RfidCardService : IRfidCardService
{
    private readonly AppDbContext _db;

    public RfidCardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<RfidCardDto>> GetAllAsync() =>
        await _db.RfidCards
            .Include(c => c.User)
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => ToDto(c))
            .ToListAsync();

    public async Task<IEnumerable<RfidCardDto>> GetUnassignedAsync() =>
        await _db.RfidCards
            .Where(c => !c.IsAssigned)
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => ToDto(c))
            .ToListAsync();

    public async Task<RfidCardDto?> RegisterAsync(RegisterCardRequest request)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == request.UserId))
            return null; // user not found

        var card = await _db.RfidCards
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Uid == request.Uid);

        if (card is null)
        {
            // brand-new UID → create assigned directly
            card = new RfidCard
            {
                Uid = request.Uid,
                UserId = request.UserId,
                IsAssigned = true,
                IsActive = true,
            };
            _db.RfidCards.Add(card);
        }
        else
        {
            // already exists (likely auto-stored unassigned) → assign it
            card.UserId = request.UserId;
            card.IsAssigned = true;
            card.IsActive = true;
        }

        await _db.SaveChangesAsync();
        await _db.Entry(card).Reference(c => c.User).LoadAsync();
        return ToDto(card);
    }

    public async Task<RfidCardDto?> AssignAsync(int cardId, int userId)
    {
        var card = await _db.RfidCards.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return null;
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) return null;

        card.UserId = userId;
        card.IsAssigned = true;
        card.IsActive = true;
        await _db.SaveChangesAsync();
        await _db.Entry(card).Reference(c => c.User).LoadAsync();
        return ToDto(card);
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var card = await _db.RfidCards.FindAsync(id);
        if (card is null) return false;

        card.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CardValidationResult> ValidateAsync(string uid)
    {
        var card = await _db.RfidCards
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Uid == uid && c.IsActive && c.IsAssigned
                                      && c.User != null && c.User.IsActive);

        if (card is null || card.User is null)
            return new CardValidationResult(false, null, null, null, null);

        var display = string.IsNullOrWhiteSpace(card.User.FullName)
            ? card.User.Username : card.User.FullName;
        return new CardValidationResult(true, card.UserId, card.User.Username, display, card.User.Role);
    }

    public async Task EnsureCardExistsAsync(string uid)
    {
        if (await _db.RfidCards.AnyAsync(c => c.Uid == uid))
            return; // already known (assigned or not) — no duplicate

        _db.RfidCards.Add(new RfidCard
        {
            Uid = uid,
            IsAssigned = false,
            IsActive = true,
            UserId = null,
        });
        await _db.SaveChangesAsync();
    }

    private static RfidCardDto ToDto(RfidCard c) =>
        new(c.Id, c.Uid, c.IsActive, c.IsAssigned, c.RegisteredAt, c.UserId, c.User?.Username);
}
