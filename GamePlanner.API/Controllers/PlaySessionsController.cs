using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
public class PlaySessionsController(PlaySessionManager manager) : ControllerBase
{
    [HttpGet("api/games/{gameId:int}/sessions")]
    public async Task<IActionResult> GetForGame(int gameId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetForGameAsync(userId.Value, gameId));
    }

    [HttpPost("api/games/{gameId:int}/sessions")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Log(int gameId, [FromBody] LogSessionRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var id = await manager.LogAsync(userId.Value, gameId, request);
            return id is null ? NotFound() : Ok(new { id });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("api/sessions/{id:int}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Update(int id, [FromBody] LogSessionRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return await manager.UpdateAsync(userId.Value, id, request) ? NoContent() : NotFound();
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("api/sessions/{id:int}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await manager.DeleteAsync(userId.Value, id) ? NoContent() : NotFound();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
