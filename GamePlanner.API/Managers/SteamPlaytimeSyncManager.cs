using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class SteamPlaytimeSyncManager(
    GamePlannerDbContext db,
    SteamService steam,
    IConfiguration config,
    ILogger<SteamPlaytimeSyncManager> logger)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public async Task RunAsync(CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => u.IsActive && u.SteamId != null)
            .Select(u => new { u.Id, u.SteamId })
            .ToListAsync(ct);

        if (users.Count == 0)
        {
            logger.LogInformation("No users with SteamId linked. Skipping playtime sync.");
            return;
        }

        logger.LogInformation("Syncing Steam playtime for {Count} user(s).", users.Count);

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();
            await SyncUserAsync(user.Id, user.SteamId!, conn, ct);
        }
    }

    private async Task SyncUserAsync(int userId, string steamId, SqlConnection conn, CancellationToken ct)
    {
        try
        {
            var steamGames = await steam.GetOwnedGamesAsync(steamId, ct);

            var existing = await db.Games
                .Where(g => g.IsActive && g.UserId == userId && g.SteamAppId != null)
                .Select(g => new { g.Id, g.SteamAppId, g.PlaytimeHours })
                .ToListAsync(ct);

            var byAppId = existing.ToDictionary(g => g.SteamAppId!.Value, g => g);

            int updated = 0;
            foreach (var game in steamGames)
            {
                if (!byAppId.TryGetValue(game.AppId, out var row)) continue;
                if (row.PlaytimeHours == game.PlaytimeHours) continue;

                await conn.ExecuteAsync("sp_UpdateGameFromSteam",
                    new { Id = row.Id, SteamAppId = game.AppId, PlaytimeHours = game.PlaytimeHours, UserId = userId },
                    commandType: System.Data.CommandType.StoredProcedure);

                updated++;
            }

            logger.LogInformation("User {UserId}: updated playtime for {Count}/{Total} games.",
                userId, updated, existing.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync Steam playtime for user {UserId}.", userId);
        }
    }
}
