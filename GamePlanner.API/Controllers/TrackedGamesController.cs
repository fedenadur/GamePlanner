using System.Security.Claims;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/tracked-games")]
public class TrackedGamesController(TrackedGamesManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetAllAsync(userId.Value, ct));
    }

    [HttpPost("{igdbId:long}")]
    public async Task<IActionResult> Track(long igdbId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.TrackAsync(userId.Value, igdbId);
        return NoContent();
    }

    [HttpDelete("{igdbId:long}")]
    public async Task<IActionResult> Untrack(long igdbId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await manager.UntrackAsync(userId.Value, igdbId);
        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
