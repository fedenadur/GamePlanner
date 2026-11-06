using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/now-playing")]
public class NowPlayingController(NowPlayingManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSlots()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetSlotsAsync(userId.Value));
    }

    [HttpPut("{device}")]
    public async Task<IActionResult> Start(string device, [FromBody] StartNowPlayingRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return ToActionResult(await manager.StartAsync(userId.Value, device, request));
    }

    [HttpPost("{device}/remove")]
    public async Task<IActionResult> Remove(string device, [FromBody] RemoveNowPlayingRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return ToActionResult(await manager.RemoveAsync(userId.Value, device, request));
    }

    private IActionResult ToActionResult(NowPlayingResult result) => result switch
    {
        NowPlayingResult.Ok                => NoContent(),
        NowPlayingResult.GameNotFound      => NotFound(),
        NowPlayingResult.SlotEmpty         => NotFound(),
        NowPlayingResult.SlotOccupied      => Conflict(new { code = "slot_occupied" }),
        NowPlayingResult.GameOnOtherDevice => Conflict(new { code = "game_on_other_device" }),
        _                                  => BadRequest()
    };

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
