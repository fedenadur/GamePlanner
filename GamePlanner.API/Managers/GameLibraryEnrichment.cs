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

    /// <summary>HLTB main-story hours per game id: SteamAppId match first, then name. One query.</summary>
    public static async Task<Dictionary<int, double>> LoadHltbMainStoryAsync(GamePlannerDbContext db, List<Game> games, CancellationToken ct = default)
    {
        if (games.Count == 0) return [];
        var steamIds = games.Where(g => g.SteamAppId.HasValue).Select(g => g.SteamAppId!.Value).Distinct().ToList();
        var names = games.Select(g => g.Name.ToLower()).Distinct().ToList();
        var rows = await db.GamesHLTB.AsNoTracking()
            .Where(h => h.MainStoryHours != null
                        && ((h.SteamAppId.HasValue && steamIds.Contains(h.SteamAppId.Value)) || names.Contains(h.Name.ToLower())))
            .Select(h => new { h.Name, h.SteamAppId, h.MainStoryHours })
            .ToListAsync(ct);
        var bySteam = rows.Where(h => h.SteamAppId.HasValue)
            .GroupBy(h => h.SteamAppId!.Value).ToDictionary(x => x.Key, x => x.First().MainStoryHours!.Value);
        var byName = rows.GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().MainStoryHours!.Value, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<int, double>();
        foreach (var game in games)
        {
            if (game.SteamAppId is int appId && bySteam.TryGetValue(appId, out var steamHours)) result[game.Id] = steamHours;
            else if (byName.TryGetValue(game.Name, out var nameHours)) result[game.Id] = nameHours;
        }
        return result;
    }

    public static GameDto ToLibraryDto(Game game, Dictionary<int, string> journal, Dictionary<int, (double Hours, int Count)> sessions,
        Dictionary<int, double> hltbMainStory)
    {
        var (hours, count) = sessions.GetValueOrDefault(game.Id);
        double? mainStory = hltbMainStory.TryGetValue(game.Id, out var h) ? h : null;
        return game.ToDto(journal.GetValueOrDefault(game.Id, JournalStatuses.Backlog), hours, count, mainStory);
    }
}
