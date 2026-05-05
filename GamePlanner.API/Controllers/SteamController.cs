using System.Security.Claims;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SteamController(ImportJobManager jobManager) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] SteamImportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SteamId))
            return BadRequest("SteamId is required.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Prevent duplicate concurrent imports
        var active = await jobManager.GetActivePendingAsync(userId.Value, ct);
        if (active is not null)
            return Ok(new { jobId = active.Id, alreadyRunning = true });

        var job = await jobManager.CreateAsync(userId.Value, request.SteamId, ct);
        return Accepted(new { jobId = job.Id });
    }

    [HttpGet("import/{jobId:int}")]
    public async Task<IActionResult> GetProgress(int jobId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var job = await jobManager.GetForUserAsync(jobId, userId.Value, ct);
        if (job is null) return NotFound();

        return Ok(new
        {
            job.Status,
            job.Progress,
            job.ProcessedGames,
            job.TotalGames,
            job.ErrorMessage
        });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}

public record SteamImportRequest(string SteamId);
