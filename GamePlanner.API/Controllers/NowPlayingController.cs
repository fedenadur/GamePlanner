using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/now-playing")]
public class NowPlayingController(NowPlayingManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSlots()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetSlotsAsync(userId.Value));
    }

    [HttpPut("{slot:int}")]
    public async Task<IActionResult> SetSlot(int slot, [FromBody] SetNowPlayingRequest request)
    {
        if (slot < 0 || slot > 2) return BadRequest("Slot must be 0, 1, or 2.");
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.SetSlotAsync(userId.Value, slot, request.GameId);
        return NoContent();
    }

    [HttpDelete("{slot:int}")]
    public async Task<IActionResult> ClearSlot(int slot)
    {
        if (slot < 0 || slot > 2) return BadRequest("Slot must be 0, 1, or 2.");
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.ClearSlotAsync(userId.Value, slot);
        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
