using System.Security.Claims;
using System.Text.Json;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CalendarController(GamePlannerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int    year,
        [FromQuery] int    month,
        [FromQuery] int?   week,
        [FromQuery] string? genre,
        [FromQuery] int?   hypeMin,
        [FromQuery] bool   ownedOnly           = false,
        [FromQuery] bool   favoriteGenresOnly  = false,
        CancellationToken  ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        DateTime from, to;

        if (week.HasValue)
        {
            var firstOfMonth = new DateTime(year, month, 1);
            var startOfWeek1 = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
            from = startOfWeek1.AddDays((week.Value - 1) * 7);
            to   = from.AddDays(7).AddTicks(-1);
        }
        else
        {
            from = new DateTime(year, month, 1);
            to   = from.AddMonths(1).AddTicks(-1);
        }

        var query = db.UpcomingGames
            .Where(g => g.ReleaseDate >= from && g.ReleaseDate <= to);

        if (hypeMin.HasValue)
            query = query.Where(g => g.Hypes >= hypeMin.Value);

        var games = await query
            .OrderByDescending(g => g.Hypes)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(genre))
            games = games.Where(g => ParseGenres(g.Genres)
                .Any(gr => gr.Equals(genre, StringComparison.OrdinalIgnoreCase))).ToList();

        var igdbIds = games.Select(g => g.IGDBId).ToList();

        var localMap = (await db.Games
            .Where(g => g.IsActive && g.UserId == userId && g.IGDBId != null && igdbIds.Contains(g.IGDBId!.Value))
            .Select(g => new { g.IGDBId, g.Id })
            .ToListAsync(ct))
            .ToDictionary(g => g.IGDBId!.Value, g => g.Id);

        var trackedSet = (await db.TrackedGames
            .Where(t => t.UserId == userId && igdbIds.Contains(t.IGDBId))
            .Select(t => t.IGDBId)
            .ToListAsync(ct))
            .ToHashSet();

        var userGenreSet = new HashSet<string>(
            await db.Games
                .Where(g => g.UserId == userId && g.IsActive && g.Genres.Any())
                .SelectMany(g => g.Genres.Select(gen => gen.Name))
                .Distinct()
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        if (ownedOnly)
            games = games.Where(g => localMap.ContainsKey(g.IGDBId)).ToList();

        if (favoriteGenresOnly && userGenreSet.Count > 0)
            games = games.Where(g => ParseGenres(g.Genres)
                .Any(gr => userGenreSet.Contains(gr))).ToList();

        var grouped = games
            .GroupBy(g => g.ReleaseDate.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Select(x =>
                {
                    var genres = ParseGenres(x.Genres);
                    return new CalendarGameDto
                    {
                        IGDBId      = x.IGDBId,
                        Name        = x.Name,
                        CoverUrl    = x.CoverUrl,
                        Hypes       = x.Hypes,
                        LocalGameId = localMap.TryGetValue(x.IGDBId, out var id) ? id : null,
                        Genres      = genres,
                        GenreMatch  = userGenreSet.Count > 0 && genres.Any(g => userGenreSet.Contains(g)),
                        IsTracked   = trackedSet.Contains(x.IGDBId)
                    };
                }).ToList()
            );

        return Ok(grouped);
    }

    private static string[] ParseGenres(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
