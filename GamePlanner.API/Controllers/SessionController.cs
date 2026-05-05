using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SessionController(SessionManager manager, SessionPlannerManager planner) : ControllerBase
{
    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] SessionRequest request)
    {
        if (request.AvailableMinutes <= 0 || string.IsNullOrWhiteSpace(request.Mood))
            return BadRequest("AvailableMinutes and Mood are required.");

        var result = await manager.GetRecommendationAsync(request);
        return Ok(result);
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Plan([FromBody] SessionPlannerRequest request, CancellationToken ct)
    {
        var result = await planner.GetRecommendationsAsync(request, ct);
        return Ok(result);
    }
}
