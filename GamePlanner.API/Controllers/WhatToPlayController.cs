using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers.WhatToPlay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/what-to-play")]
public class WhatToPlayController(WhatToPlayManager manager) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Recommend([FromBody] WhatToPlayRequest request, [FromQuery] int take = 5, CancellationToken ct = default)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        try
        {
            return Ok(await manager.RecommendAsync(userId, request, Math.Clamp(take, 1, 10), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
