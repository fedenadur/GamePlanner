using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameImportManager(
    GamePlannerDbContext db,
    SteamService steam,
    GameEnrichmentManager enrichment,
    IConfiguration config,
    ILogger<GameImportManager> logger)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    private static readonly string[] NonGameSuffixes =
    [
        " demo", " beta", " playtest", " play test",
        " technical test", " tech test", " free weekend",
        " trial", " benchmark", " dedicated server", " sdk",
        " soundtrack", " ost"
    ];

    private static bool IsNonGame(string name)
    {
        var lower = name.ToLowerInvariant();
        return NonGameSuffixes.Any(s => lower.EndsWith(s) || lower.Contains(s + " "));
    }

    public async Task<int> ImportFromSteamAsync(string steamInput, int userId, CancellationToken ct)
    {
        var allGames   = await steam.GetOwnedGamesAsync(steamInput, ct);
        var steamGames = allGames.Where(g => !IsNonGame(g.Name)).ToList();

        var skipped = allGames.Count - steamGames.Count;
        logger.LogInformation("Fetched {Count} games from Steam. Skipped {Skipped} non-games (demos, betas, etc.).",
            steamGames.Count, skipped);

        var existing = await db.Games
            .Where(g => g.IsActive && g.UserId == userId)
            .Select(g => new { g.Id, g.Name, g.SteamAppId })
            .ToListAsync(ct);

        var byAppId = existing
            .Where(g => g.SteamAppId.HasValue)
            .GroupBy(g => g.SteamAppId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var byName = existing
            .GroupBy(g => g.Name.ToLower())
            .ToDictionary(g => g.Key, g => g.First().Id);

        int imported = 0;
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        foreach (var game in steamGames)
        {
            ct.ThrowIfCancellationRequested();

            int? matchedId = null;
            if (byAppId.TryGetValue(game.AppId, out var idByAppId))
                matchedId = idByAppId;
            else if (byName.TryGetValue(game.Name.ToLower(), out var idByName))
                matchedId = idByName;

            if (matchedId.HasValue)
            {
                await conn.ExecuteAsync("sp_UpdateGameFromSteam",
                    new { Id = matchedId.Value, SteamAppId = game.AppId, PlaytimeHours = game.PlaytimeHours, UserId = userId },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
            else
            {
                await conn.ExecuteAsync("sp_InsertSteamGame",
                    new { Name = game.Name, SteamAppId = game.AppId, PlaytimeHours = game.PlaytimeHours, UserId = userId },
                    commandType: System.Data.CommandType.StoredProcedure);

                byName[game.Name.ToLower()] = -1;
            }

            imported++;
        }

        logger.LogInformation("Steam import complete. {Count} games processed.", imported);
        var (enrichFailedCount, _) = await enrichment.EnrichAsync(ct);
        if (enrichFailedCount > 0)
            logger.LogWarning("Steam import: {FailedCount} game(s) failed enrichment.", enrichFailedCount);
        return imported;
    }

    public async Task ImportWithProgressAsync(ImportJob job, CancellationToken ct)
    {
        const int batchSize = 25;

        var allGames   = await steam.GetOwnedGamesAsync(job.SteamId, ct);
        var steamGames = allGames.Where(g => !IsNonGame(g.Name)).ToList();

        logger.LogInformation("Job {Id}: {Count} games to import.", job.Id, steamGames.Count);

        job.TotalGames = steamGames.Count;
        await db.SaveChangesAsync(ct);

        if (steamGames.Count == 0) return;

        // Scope lookup to this user only
        var existing = await db.Games
            .Where(g => g.IsActive && g.UserId == job.UserId)
            .Select(g => new { g.Id, g.Name, g.SteamAppId })
            .ToListAsync(ct);

        var byAppId = existing
            .Where(g => g.SteamAppId.HasValue)
            .GroupBy(g => g.SteamAppId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var byName = existing
            .GroupBy(g => g.Name.ToLower())
            .ToDictionary(g => g.Key, g => g.First().Id);

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        int processed = 0;

        for (int i = 0; i < steamGames.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var game in steamGames.Skip(i).Take(batchSize))
            {
                int? matchedId = null;
                if (byAppId.TryGetValue(game.AppId, out var idByAppId))
                    matchedId = idByAppId;
                else if (byName.TryGetValue(game.Name.ToLower(), out var idByName))
                    matchedId = idByName;

                if (matchedId.HasValue)
                {
                    await conn.ExecuteAsync("sp_UpdateGameFromSteam",
                        new { Id = matchedId.Value, SteamAppId = game.AppId, PlaytimeHours = game.PlaytimeHours, UserId = job.UserId },
                        commandType: System.Data.CommandType.StoredProcedure);
                }
                else
                {
                    await conn.ExecuteAsync("sp_InsertSteamGame",
                        new { Name = game.Name, SteamAppId = game.AppId, PlaytimeHours = game.PlaytimeHours, UserId = job.UserId },
                        commandType: System.Data.CommandType.StoredProcedure);

                    byName[game.Name.ToLower()] = -1;
                }

                processed++;
            }

            job.ProcessedGames = processed;
            job.Progress       = (int)Math.Round(processed * 100.0 / steamGames.Count);
            await db.SaveChangesAsync(ct);

            logger.LogDebug("Job {Id}: {Processed}/{Total} games processed.", job.Id, processed, steamGames.Count);
        }
    }
}
