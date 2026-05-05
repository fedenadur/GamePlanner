using System.Security.Claims;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/backlog")]
public class BacklogPrioritizerController(BacklogPrioritizerManager manager) : ControllerBase
{
    [HttpGet("prioritize")]
    public async Task<IActionResult> Prioritize(
        [FromQuery] string? mode,
        [FromQuery] int limit = 10)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (limit is < 1 or > 50) limit = 10;

        var results = await manager.PrioritizeAsync(userId.Value, mode, limit);
        return Ok(results);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
