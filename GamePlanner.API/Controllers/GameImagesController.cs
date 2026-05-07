using System.Security.Claims;
using GamePlanner.API.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GameImagesController(GamePlannerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string type    = "screenshot",
        [FromQuery] long?  igdbId  = null,
        CancellationToken  ct      = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var query = db.GameImages
            .Where(i => i.Game.UserId == userId && i.Game.IsActive && i.ImageType == type);

        if (igdbId.HasValue)
            query = query.Where(i => i.Game.IGDBId == igdbId.Value);

        var images = await query
            .OrderBy(i => i.GameId).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.GameId, GameName = i.Game.Name, i.Url, i.ImageType })
            .ToListAsync(ct);

        return Ok(images);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
