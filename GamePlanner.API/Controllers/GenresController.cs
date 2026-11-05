using System.Security.Claims;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GenresController(UserSettingsManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return Unauthorized();
        return Ok(await manager.GetUserGenresAsync(userId));
    }
}
