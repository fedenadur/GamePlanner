using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameImageManager(
    GamePlannerDbContext db,
    IgdbService igdb,
    IConfiguration config,
    ILogger<GameImageManager> logger)
{
    private const int BatchSize = 50;

    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public async Task RunAsync(CancellationToken ct)
    {
        var total = await db.Games.CountAsync(g => g.IsActive && g.CoverImageUrl == null, ct);

        if (total == 0)
        {
            logger.LogInformation("GameImageManager: no games need cover images.");
            return;
        }

        logger.LogInformation("GameImageManager: {Total} games need cover images.", total);

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        int processed = 0;

        while (!ct.IsCancellationRequested)
        {
            // Always query from offset 0 — processed games drop out of the filter
            var batch = await db.Games
                .Where(g => g.IsActive && g.CoverImageUrl == null)
                .OrderBy(g => g.Id)
                .Take(BatchSize)
                .Select(g => new { g.Id, g.Name, g.IGDBId, g.IGDBUpdatedAt })
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            // Split into games with known IGDB IDs (batch fetch) and unknowns (name search)
            var withId    = batch.Where(g => g.IGDBId.HasValue).ToList();
            var withoutId = batch.Where(g => !g.IGDBId.HasValue).ToList();

            // 1. Batch fetch covers for all games that have an IGDB ID — 1 API call
            Dictionary<long, IgdbGame> igdbResults = [];
            if (withId.Count > 0)
            {
                try
                {
                    var fetched = await igdb.FetchCoversByIdsAsync(withId.Select(g => g.IGDBId!.Value), ct);
                    igdbResults = fetched.ToDictionary(r => r.Id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Batch cover fetch failed, will retry individually.");
                }
            }

            // 2. Apply batch results
            foreach (var game in withId)
            {
                ct.ThrowIfCancellationRequested();
                igdbResults.TryGetValue(game.IGDBId!.Value, out var result);
                await SaveCoverAsync(conn, game.Id, game.Name, game.IGDBUpdatedAt, result);
            }

            // 3. Name-search games individually (no batch API for this)
            foreach (var game in withoutId)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await igdb.SearchCoverByNameAsync(game.Name, ct);
                    await SaveCoverAsync(conn, game.Id, game.Name, game.IGDBUpdatedAt, result);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Cover search failed for '{Name}', skipping.", game.Name);
                }
                await Task.Delay(250, ct); // rate limit only for individual searches
            }

            processed += batch.Count;
            logger.LogInformation("GameImageManager: {Processed}/{Total} processed.", processed, total);

            if (batch.Count < BatchSize) break;
        }

        logger.LogInformation("GameImageManager: cover sync complete.");
    }

    private async Task SaveCoverAsync(
        SqlConnection conn, int gameId, string name,
        DateTime? currentUpdatedAt, IgdbGame? result)
    {
        if (result?.Cover is null)
        {
            logger.LogDebug("No cover found for '{Name}'.", name);
            return;
        }

        var igdbUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(result.UpdatedAt).UtcDateTime;

        if (currentUpdatedAt.HasValue && igdbUpdatedAt <= currentUpdatedAt.Value)
        {
            logger.LogDebug("Cover up-to-date for '{Name}', skipping.", name);
            return;
        }

        var url = IgdbService.OptimizeCoverUrl(result.Cover.Url);

        await conn.ExecuteAsync("sp_UpdateGameCover",
            new { Id = gameId, CoverImageUrl = url, IGDBUpdatedAt = igdbUpdatedAt },
            commandType: System.Data.CommandType.StoredProcedure);

        logger.LogDebug("Cover saved for '{Name}'.", name);
    }
}
