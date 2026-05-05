using System.Security.Claims;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var result = await manager.GetDashboardAsync(userId);
        return Ok(result);
    }
}
