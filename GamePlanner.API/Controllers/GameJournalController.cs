using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/journal")]
public class GameJournalController(GameJournalManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetJournal()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetJournalAsync(userId.Value));
    }

    [HttpGet("{gameId:int}")]
    public async Task<IActionResult> GetEntry(int gameId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var entry = await manager.GetEntryAsync(userId.Value, gameId);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost("{gameId:int}")]
    public async Task<IActionResult> CreateEntry(int gameId, [FromBody] UpsertJournalRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.UpsertAsync(userId.Value, gameId, request);
        return CreatedAtAction(nameof(GetEntry), new { gameId }, null);
    }

    [HttpPut("{gameId:int}")]
    public async Task<IActionResult> UpdateEntry(int gameId, [FromBody] UpsertJournalRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.UpsertAsync(userId.Value, gameId, request);
        return NoContent();
    }

    [HttpDelete("{gameId:int}")]
    public async Task<IActionResult> DeleteEntry(int gameId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.DeleteAsync(userId.Value, gameId);
        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
