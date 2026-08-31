using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SessionController(SessionManager manager, SessionPlannerManager planner) : ControllerBase
{
    [HttpPost("recommend")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Recommend([FromBody] SessionRequest request)
    {
        if (request.AvailableMinutes <= 0 || string.IsNullOrWhiteSpace(request.Mood))
            return BadRequest("AvailableMinutes and Mood are required.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await manager.GetRecommendationAsync(request, userId.Value);
        return Ok(result);
    }

    [HttpPost("plan")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Plan([FromBody] SessionPlannerRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await planner.GetRecommendationsAsync(request, userId.Value, ct);
        return Ok(result);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
