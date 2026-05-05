using System.Text.Json;
using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class TrackedGamesManager(GamePlannerDbContext db, IConfiguration config)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public async Task<List<TrackedGameDto>> GetAllAsync(int userId, CancellationToken ct)
    {
        var rows = await (
            from t in db.TrackedGames
            where t.UserId == userId
            join u in db.UpcomingGames on t.IGDBId equals u.IGDBId
            select new { t.IGDBId, t.TrackedAt, u.Name, u.CoverUrl, u.ReleaseDate, u.Hypes, u.Genres }
        ).ToListAsync(ct);

        var igdbIds = rows.Select(r => r.IGDBId).ToList();

        var localMap = (await db.Games
            .Where(g => g.IsActive && g.UserId == userId && g.IGDBId != null && igdbIds.Contains(g.IGDBId!.Value))
            .Select(g => new { g.IGDBId, g.Id })
            .ToListAsync(ct))
            .ToDictionary(g => g.IGDBId!.Value, g => g.Id);

        return rows
            .OrderBy(r => r.ReleaseDate)
            .Select(r => new TrackedGameDto
            {
                IGDBId      = r.IGDBId,
                Name        = r.Name,
                CoverUrl    = r.CoverUrl,
                ReleaseDate = r.ReleaseDate,
                Hypes       = r.Hypes,
                Genres      = ParseGenres(r.Genres),
                LocalGameId = localMap.TryGetValue(r.IGDBId, out var id) ? id : null,
                TrackedAt   = r.TrackedAt
            })
            .ToList();
    }

    public async Task<HashSet<long>> GetTrackedIgdbIdsAsync(int userId, CancellationToken ct) =>
        (await db.TrackedGames
            .Where(t => t.UserId == userId)
            .Select(t => t.IGDBId)
            .ToListAsync(ct))
        .ToHashSet();

    public async Task TrackAsync(int userId, long igdbId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_InsertTrackedGame",
            new { UserId = userId, IGDBId = igdbId },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task UntrackAsync(int userId, long igdbId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_DeleteTrackedGame",
            new { UserId = userId, IGDBId = igdbId },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    private static string[] ParseGenres(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];
}
