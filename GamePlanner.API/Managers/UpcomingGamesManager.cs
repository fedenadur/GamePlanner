using System.Text.Json;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class UpcomingGamesManager(GamePlannerDbContext db, ILogger<UpcomingGamesManager> logger)
{
    public async Task UpsertAsync(List<IgdbGame> games, CancellationToken ct)
    {
        int inserted = 0, updated = 0, skipped = 0;

        foreach (var g in games)
        {
            ct.ThrowIfCancellationRequested();

            if (!g.FirstReleaseDate.HasValue) continue;

            var releaseDate = DateTimeOffset.FromUnixTimeSeconds(g.FirstReleaseDate.Value).UtcDateTime;
            var coverUrl    = g.Cover is not null ? IgdbService.OptimizeCoverUrl(g.Cover.Url) : null;
            var genres      = JsonSerializer.Serialize(g.Genres?.Select(x => x.Name).ToList() ?? []);
            var hypes       = g.Hypes ?? 0;

            var existing = await db.UpcomingGames.FirstOrDefaultAsync(u => u.IGDBId == g.Id, ct);

            if (existing is null)
            {
                db.UpcomingGames.Add(new UpcomingGame
                {
                    IGDBId      = g.Id,
                    Name        = g.Name,
                    CoverUrl    = coverUrl,
                    ReleaseDate = releaseDate,
                    Hypes       = hypes,
                    Genres      = genres,
                    CachedAt    = DateTime.UtcNow
                });
                inserted++;
            }
            else if (existing.Hypes != hypes || existing.CoverUrl != coverUrl || existing.ReleaseDate != releaseDate)
            {
                existing.Name        = g.Name;
                existing.CoverUrl    = coverUrl;
                existing.ReleaseDate = releaseDate;
                existing.Hypes       = hypes;
                existing.Genres      = genres;
                existing.CachedAt    = DateTime.UtcNow;
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("UpcomingGames upsert: {Inserted} inserted, {Updated} updated, {Skipped} skipped.", inserted, updated, skipped);
    }
}
