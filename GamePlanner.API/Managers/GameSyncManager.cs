using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameSyncManager(
    GamePlannerDbContext db,
    IgdbService igdb,
    ILogger<GameSyncManager> logger)
{
    private const int BatchSize = 50;
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(250);

    // Needs enrichment: missing collections OR missing rating/release data
    private static bool NeedsEnrichment(Game g) =>
        !g.Genres.Any() || !g.Tags.Any() || !g.Themes.Any()
        || !g.PlayerPerspectives.Any() || !g.GameModes.Any()
        || g.AggregatedRating is null || g.FirstReleaseDate is null;

    public async Task RunAsync(CancellationToken ct)
    {
        var total = await db.Games
            .CountAsync(g => g.IsActive &&
                (!g.Genres.Any() || !g.Tags.Any() || !g.Themes.Any()
                 || !g.PlayerPerspectives.Any() || !g.GameModes.Any()
                 || g.AggregatedRating == null || g.FirstReleaseDate == null), ct);

        if (total == 0)
        {
            logger.LogInformation("All games are already enriched. Skipping sync.");
            return;
        }

        logger.LogInformation("Starting enrichment for {Total} games with missing metadata.", total);

        int offset = 0;
        int processed = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Games
                .Where(g => g.IsActive &&
                    (!g.Genres.Any() || !g.Tags.Any() || !g.Themes.Any()
                     || !g.PlayerPerspectives.Any() || !g.GameModes.Any()
                     || g.AggregatedRating == null || g.FirstReleaseDate == null))
                .Select(g => new { g.Id, g.IGDBId, g.Name, g.IGDBUpdatedAt })
                .OrderBy(g => g.Id)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            foreach (var item in batch)
            {
                ct.ThrowIfCancellationRequested();
                await EnrichGameAsync(item.Id, item.IGDBId, item.Name, item.IGDBUpdatedAt, ct);
                await Task.Delay(RequestDelay, ct);
            }

            processed += batch.Count;
            logger.LogInformation("Progress: {Processed}/{Total} games processed.", processed, total);

            if (batch.Count < BatchSize) break;
            offset += BatchSize;
        }

        logger.LogInformation("Enrichment sync complete. {Processed} games processed.", processed);
    }

    private async Task EnrichGameAsync(int id, long? igdbId, string name, DateTime? storedUpdatedAt, CancellationToken ct)
    {
        try
        {
            var result = igdbId.HasValue
                ? await igdb.FetchEnrichmentByIdAsync(igdbId.Value, ct)
                : await igdb.SearchGameAsync(name, ct);

            if (result is null) return;

            // Step 3: Skip if IGDB hasn't changed since last sync
            var igdbUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(result.UpdatedAt).UtcDateTime;
            if (storedUpdatedAt.HasValue && igdbUpdatedAt <= storedUpdatedAt.Value)
            {
                logger.LogDebug("'{Name}' is up-to-date (IGDB updated_at unchanged). Skipping.", name);
                return;
            }

            var genreNames       = result.Genres?.Select(g => g.Name).ToList() ?? [];
            var themeNames       = result.Themes?.Select(t => t.Name).ToList() ?? [];
            var perspectiveNames = result.PlayerPerspectives?.Select(p => p.Name).ToList() ?? [];
            var modeNames        = result.GameModes?.Select(m => m.Name).ToList() ?? [];

            var game = await db.Games
                .Include(g => g.Genres)
                .Include(g => g.Tags)
                .Include(g => g.Themes)
                .Include(g => g.PlayerPerspectives)
                .Include(g => g.GameModes)
                .FirstAsync(g => g.Id == id, ct);

            // Collections — only fill if empty
            if (!game.Genres.Any())
                await SyncCollectionAsync(game.Genres, genreNames,
                    n => db.Genres.FirstOrDefaultAsync(x => x.Name == n, ct),
                    n => new Genre { Name = n }, ct);

            if (!game.Tags.Any())
                await SyncCollectionAsync(game.Tags, themeNames,
                    n => db.Tags.FirstOrDefaultAsync(x => x.Name == n, ct),
                    n => new Tag { Name = n }, ct);

            if (!game.Themes.Any())
                await SyncCollectionAsync(game.Themes, themeNames,
                    n => db.Themes.FirstOrDefaultAsync(x => x.Name == n, ct),
                    n => new Theme { Name = n }, ct);

            if (!game.PlayerPerspectives.Any())
                await SyncCollectionAsync(game.PlayerPerspectives, perspectiveNames,
                    n => db.PlayerPerspectives.FirstOrDefaultAsync(x => x.Name == n, ct),
                    n => new PlayerPerspective { Name = n }, ct);

            if (!game.GameModes.Any())
                await SyncCollectionAsync(game.GameModes, modeNames,
                    n => db.GameModes.FirstOrDefaultAsync(x => x.Name == n, ct),
                    n => new GameMode { Name = n }, ct);

            // Step 4: Update scalar fields — only overwrite nulls or when IGDB has new data
            if (result.AggregatedRating.HasValue)
                game.AggregatedRating = result.AggregatedRating;

            if (result.AggregatedRatingCount.HasValue)
                game.AggregatedRatingCount = result.AggregatedRatingCount;

            if (result.FirstReleaseDate.HasValue)
                game.FirstReleaseDate = DateTimeOffset.FromUnixTimeSeconds(result.FirstReleaseDate.Value).UtcDateTime;

            // Track sync timestamps
            game.IGDBUpdatedAt  = igdbUpdatedAt;
            game.LastIGDBSyncAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Enriched '{Name}': {G} genres, rating={R}, release={D}.",
                name, genreNames.Count,
                result.AggregatedRating?.ToString("F1") ?? "n/a",
                game.FirstReleaseDate?.Year.ToString() ?? "n/a");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enrich '{Name}', skipping.", name);
        }
    }

    private async Task SyncCollectionAsync<TEntity>(
        List<TEntity> collection,
        List<string> names,
        Func<string, Task<TEntity?>> finder,
        Func<string, TEntity> factory,
        CancellationToken ct) where TEntity : class
    {
        foreach (var name in names)
        {
            ct.ThrowIfCancellationRequested();
            var entity = await finder(name) ?? db.Set<TEntity>().Add(factory(name)).Entity;
            collection.Add(entity);
        }
    }
}
