using System.Security.Claims;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using GamePlanner.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GamesController(GameManager manager, IgdbService igdb) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await manager.GetAllAsync(userId.Value));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var game = await manager.GetByIdAsync(id, userId.Value);
        return game is null ? NotFound() : Ok(game);
    }

    [HttpGet("{id}/detail")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var detail = await manager.GetDetailAsync(id, userId.Value);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("igdb/search")]
    public async Task<IActionResult> SearchIgdb([FromQuery] string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");
        var results = await igdb.SearchMultipleAsync(name, ct);
        var dto = results.Select(r => new IgdbSearchResultDto
        {
            IgdbId      = r.Id,
            Name        = r.Name,
            CoverUrl    = r.Cover?.Url is { } url ? IgdbService.OptimizeCoverUrl(url) : null,
            Genres      = r.Genres?.Select(g => g.Name).ToList() ?? [],
            ReleaseYear = r.FirstReleaseDate.HasValue
                          ? DateTimeOffset.FromUnixTimeSeconds(r.FirstReleaseDate.Value).Year
                          : null
        }).ToList();
        return Ok(dto);
    }

    [HttpPost("from-igdb")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateFromIgdb([FromBody] CreateGameFromIgdbRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var newId = await manager.CreateFromIgdbAsync(request, userId.Value);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    [HttpPost]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Create([FromBody] CreateGameRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var newId = await manager.CreateAsync(request, userId.Value);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    [HttpPut("{id}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGameRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var updated = await manager.UpdateAsync(id, request, userId.Value);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var deleted = await manager.DeleteAsync(id, userId.Value);
        return deleted ? NoContent() : NotFound();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
