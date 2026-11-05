using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using GamePlanner.API.Mapping;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

/// <summary>Journal status and logged-session totals shared by every manager that returns library GameDtos.</summary>
public static class GameLibraryEnrichment
{
    public static async Task<Dictionary<int, string>> LoadJournalStatusesAsync(GamePlannerDbContext db, int userId, List<int> gameIds) =>
        await db.GameJournalEntries
            .Where(j => j.UserId == userId && gameIds.Contains(j.GameId))
            .ToDictionaryAsync(j => j.GameId, j => j.Status);

    public static async Task<Dictionary<int, (double Hours, int Count)>> LoadSessionTotalsAsync(GamePlannerDbContext db, int userId, List<int>? gameIds = null)
    {
        var rows = await db.PlaySessions
            .Where(p => p.UserId == userId && (gameIds == null || gameIds.Contains(p.GameId)))
            .GroupBy(p => p.GameId)
            .Select(x => new { GameId = x.Key, Hours = x.Sum(p => p.Hours), Count = x.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.GameId, r => ((double)r.Hours, r.Count));
    }

    public static GameDto ToLibraryDto(Game game, Dictionary<int, string> journal, Dictionary<int, (double Hours, int Count)> sessions)
    {
        var (hours, count) = sessions.GetValueOrDefault(game.Id);
        return game.ToDto(journal.GetValueOrDefault(game.Id, JournalStatuses.Backlog), hours, count);
    }
}
