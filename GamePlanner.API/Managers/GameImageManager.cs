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

    public async Task<(int FailedCount, string? FailureDetail)> RunAsync(CancellationToken ct)
    {
        var failedCount = 0;
        var failedNames = new List<string>();

        // Images run before covers: sp_UpdateGameCover stamps LastImageSyncDate, which would otherwise
        // make this same run skip the screenshots of every row that just got its cover.
        var (imageFailedCount, imageFailedNames) = await SyncScreenshotsAndArtworksAsync(ct);
        failedCount += imageFailedCount;
        failedNames.AddRange(imageFailedNames);

        var total = await db.Games.CountAsync(g => g.IsActive && g.CoverImageUrl == null, ct);

        if (total == 0)
        {
            logger.LogInformation("GameImageManager: no games need cover images.");
        }
        else
        {
            logger.LogInformation("GameImageManager: {Total} games need cover images.", total);
            var (coverFailedCount, coverFailedNames) = await SyncCoversAsync(total, ct);
            failedCount += coverFailedCount;
            failedNames.AddRange(coverFailedNames);
        }

        return (failedCount, BuildFailureDetail(failedCount, failedNames));
    }

    private static string? BuildFailureDetail(int failedCount, List<string> failedNames)
    {
        if (failedCount == 0) return null;

        var sample = string.Join(", ", failedNames.Take(3));
        var detail = failedNames.Count > 0
            ? $"{failedCount} game(s) had incomplete image sync (e.g. {sample})"
            : $"{failedCount} game(s) had incomplete image sync";

        return detail.Length > 480 ? detail[..480] : detail;
    }

    // ── Cover sync ───────────────────────────────

    private async Task<(int FailedCount, List<string> FailedNames)> SyncCoversAsync(int total, CancellationToken ct)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        int processed = 0;
        int lastId = 0;
        int failedCount = 0;
        var failedNames = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Games
                .Where(g => g.IsActive && g.CoverImageUrl == null && g.Id > lastId)
                .OrderBy(g => g.Id)
                .Take(BatchSize)
                .Select(g => new { g.Id, g.Name, g.IGDBId })
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            var withId    = batch.Where(g => g.IGDBId.HasValue).ToList();
            var withoutId = batch.Where(g => !g.IGDBId.HasValue).ToList();

            Dictionary<long, IgdbGame> igdbResults = [];
            bool batchFailed = false;
            if (withId.Count > 0)
            {
                try
                {
                    var fetched = await igdb.FetchCoversByIdsAsync(withId.Select(g => g.IGDBId!.Value), ct);
                    igdbResults = fetched.ToDictionary(r => r.Id);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Batch failed, so each of its rows is fetched on its own below: the Id cursor does not revisit them this run.
                    logger.LogWarning(ex, "Batch cover fetch failed, will retry individually.");
                    batchFailed = true;
                }
            }

            foreach (var game in withId)
            {
                ct.ThrowIfCancellationRequested();
                IgdbGame? result;
                if (batchFailed)
                {
                    try
                    {
                        result = (await igdb.FetchCoversByIdsAsync([game.IGDBId!.Value], ct)).FirstOrDefault();
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Cover retry failed for '{Name}', skipping.", game.Name);
                        failedCount++;
                        failedNames.Add(game.Name);
                        await Task.Delay(250, ct);
                        continue;
                    }
                    await Task.Delay(250, ct);
                }
                else
                {
                    igdbResults.TryGetValue(game.IGDBId!.Value, out result);
                }
                await SaveCoverAsync(conn, game.Id, game.Name, result);
            }

            foreach (var game in withoutId)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await igdb.SearchCoverByNameAsync(game.Name, ct);
                    await SaveCoverAsync(conn, game.Id, game.Name, result);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Cover search failed for '{Name}', skipping.", game.Name);
                    failedCount++;
                    failedNames.Add(game.Name);
                }
                await Task.Delay(250, ct);
            }

            processed += batch.Count;
            lastId = batch[^1].Id;
            logger.LogInformation("GameImageManager: covers {Processed}/{Total} processed.", processed, total);

            if (batch.Count < BatchSize) break;
        }

        logger.LogInformation("GameImageManager: cover sync complete.");
        return (failedCount, failedNames);
    }

    private async Task SaveCoverAsync(
        SqlConnection conn, int gameId, string name, IgdbGame? result)
    {
        if (result?.Cover is null)
        {
            logger.LogDebug("No cover found for '{Name}'.", name);
            return;
        }

        var igdbUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(result.UpdatedAt).UtcDateTime;

        var url = IgdbService.OptimizeCoverUrl(result.Cover.Url);

        await conn.ExecuteAsync("sp_UpdateGameCover",
            new { Id = gameId, CoverImageUrl = url, IGDBUpdatedAt = igdbUpdatedAt },
            commandType: System.Data.CommandType.StoredProcedure);

        logger.LogDebug("Cover saved for '{Name}'.", name);
    }

    // ── Screenshots + artworks sync ──────────────────────────────────────────

    private async Task<(int FailedCount, List<string> FailedNames)> SyncScreenshotsAndArtworksAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-2);

        var total = await db.Games.CountAsync(
            g => g.IsActive && g.IGDBId != null
              && (g.LastImageSyncDate == null || g.LastImageSyncDate < cutoff), ct);

        if (total == 0)
        {
            logger.LogInformation("GameImageManager: no games need screenshot/artwork sync.");
            return (0, []);
        }

        logger.LogInformation("GameImageManager: {Total} games need screenshot/artwork sync.", total);

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        int processed = 0;
        int failedCount = 0;
        var failedNames = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Games
                .Where(g => g.IsActive && g.IGDBId != null
                         && (g.LastImageSyncDate == null || g.LastImageSyncDate < cutoff))
                .OrderBy(g => g.Id)
                .Take(BatchSize)
                .Select(g => new { g.Id, g.Name, g.IGDBId })
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            var igdbIds = batch.Select(g => g.IGDBId!.Value).ToList();

            List<IgdbImage> screenshots = [];
            List<IgdbImage> artworks    = [];
            bool batchFailed = false;

            try
            {
                screenshots = await FetchAllImagePagesAsync(offset => igdb.FetchScreenshotsByGameIdsAsync(igdbIds, offset, ct), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Raised from Warning: the batch's games are still marked synced below, so a
                // failed fetch here means up to BatchSize games silently go two days without images.
                logger.LogError(ex, "Screenshot fetch failed for batch, skipping screenshots.");
                batchFailed = true;
            }

            try
            {
                artworks = await FetchAllImagePagesAsync(offset => igdb.FetchArtworksByGameIdsAsync(igdbIds, offset, ct), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Artwork fetch failed for batch, skipping artworks.");
                batchFailed = true;
            }

            if (batchFailed)
            {
                // Count the whole batch once, even if both fetches failed.
                failedCount += batch.Count;
                failedNames.AddRange(batch.Select(g => g.Name).Take(3));
            }

            // Map IGDB game ID → local game IDs (an IGDBId can be shared by multiple local games)
            var igdbToLocal = batch
                .GroupBy(g => g.IGDBId!.Value)
                .ToDictionary(gr => gr.Key, gr => gr.Select(g => g.Id).ToList());

            await SaveImagesAsync(conn, screenshots, "screenshot", igdbToLocal, ct);
            await SaveImagesAsync(conn, artworks,    "artwork",    igdbToLocal, ct);

            // Mark all games in batch as synced (even if they had no images)
            foreach (var game in batch)
            {
                ct.ThrowIfCancellationRequested();
                await conn.ExecuteAsync("sp_UpdateGameLastImageSync",
                    new { GameId = game.Id },
                    commandType: System.Data.CommandType.StoredProcedure);
            }

            processed += batch.Count;
            logger.LogInformation("GameImageManager: images {Processed}/{Total} processed.", processed, total);

            if (batch.Count < BatchSize) break;
        }

        logger.LogInformation("GameImageManager: screenshot/artwork sync complete.");
        return (failedCount, failedNames);
    }

    private static async Task<List<IgdbImage>> FetchAllImagePagesAsync(
        Func<int, Task<List<IgdbImage>>> fetchPage, CancellationToken ct)
    {
        var all = new List<IgdbImage>();
        var offset = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await fetchPage(offset);
            all.AddRange(page);
            offset += page.Count;
            if (page.Count < IgdbService.ImagePageSize) break;
        }

        return all;
    }

    private async Task SaveImagesAsync(
        SqlConnection conn,
        List<IgdbImage> images,
        string imageType,
        Dictionary<long, List<int>> igdbToLocal,
        CancellationToken ct)
    {
        foreach (var image in images)
        {
            ct.ThrowIfCancellationRequested();

            if (!igdbToLocal.TryGetValue(image.Game, out var localGameIds))
                continue;

            if (string.IsNullOrEmpty(image.ImageId))
                continue;

            var url = IgdbService.OptimizeImageUrl(image.Url);

            foreach (var localGameId in localGameIds)
            {
                await conn.ExecuteAsync("sp_UpsertGameImage",
                    new
                    {
                        GameId      = localGameId,
                        IgdbImageId = image.ImageId,
                        ImageType   = imageType,
                        Url         = url,
                        Width       = image.Width,
                        Height      = image.Height
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }
    }
}
