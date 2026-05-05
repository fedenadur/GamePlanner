using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameEnrichmentManager(
    GamePlannerDbContext db,
    IgdbService igdb,
    ILogger<GameEnrichmentManager> logger)
{
    public async Task EnrichAsync(CancellationToken ct)
    {
        var games = await db.Games
            .Where(g => g.IsActive && !g.Genres.Any() && !g.Tags.Any())
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(ct);

        if (games.Count == 0)
        {
            logger.LogInformation("No games require enrichment.");
            return;
        }

        logger.LogInformation("Enriching {Count} games via IGDB.", games.Count);

        foreach (var item in games)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await igdb.SearchGameAsync(item.Name, ct);
                if (result is null)
                {
                    logger.LogDebug("No IGDB match for '{Name}'.", item.Name);
                    await Task.Delay(250, ct);
                    continue;
                }

                var genreNames = result.Genres?.Select(g => g.Name).ToList() ?? [];
                var tagNames   = result.Themes?.Select(t => t.Name).ToList() ?? [];

                if (genreNames.Count == 0 && tagNames.Count == 0)
                {
                    await Task.Delay(250, ct);
                    continue;
                }

                var game = await db.Games
                    .Include(g => g.Genres)
                    .Include(g => g.Tags)
                    .FirstAsync(g => g.Id == item.Id, ct);

                foreach (var name in genreNames)
                {
                    var genre = await db.Genres.FirstOrDefaultAsync(g => g.Name == name, ct)
                                ?? db.Genres.Add(new Genre { Name = name }).Entity;
                    if (!game.Genres.Any(g => g.Name == name))
                        game.Genres.Add(genre);
                }

                foreach (var name in tagNames)
                {
                    var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct)
                              ?? db.Tags.Add(new Tag { Name = name }).Entity;
                    if (!game.Tags.Any(t => t.Name == name))
                        game.Tags.Add(tag);
                }

                await db.SaveChangesAsync(ct);
                logger.LogDebug("Enriched '{Name}'.", item.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Enrichment failed for '{Name}', skipping.", item.Name);
            }

            await Task.Delay(250, ct);
        }

        logger.LogInformation("Enrichment complete.");
    }
}
