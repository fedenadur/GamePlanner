using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SyncController(GameSyncManager manager) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        await manager.RunAsync(ct);
        return Ok(new { message = "Sync completed." });
    }
}
