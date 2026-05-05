using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class DashboardManager(
    GamePlannerDbContext db,
    NowPlayingManager nowPlayingManager,
    BacklogPrioritizerManager backlogPrioritizerManager)
{
    public async Task<DashboardDto> GetDashboardAsync(int userId)
    {
        var username = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName ?? u.Username)
            .FirstOrDefaultAsync();

        // Continue Playing — reuse NowPlayingManager
        var slots = await nowPlayingManager.GetSlotsAsync(userId);
        var continuePlaying = slots
            .Where(s => s.Game != null)
            .Select(s => s.Game!)
            .Take(3)
            .ToList();

        // Tonight Recommendation — reuse BacklogPrioritizerManager (top 1)
        var topPick = (await backlogPrioritizerManager.PrioritizeAsync(userId, mode: null, limit: 1))
            .FirstOrDefault();

        TonightRecommendationDto? tonight = topPick is null ? null : new TonightRecommendationDto
        {
            GameId           = topPick.GameId,
            Name             = topPick.Name,
            CoverImageUrl    = topPick.CoverImageUrl,
            Reason           = topPick.Reasons.FirstOrDefault(),
            MainStoryHours   = topPick.MainStoryHours,
            AggregatedRating = topPick.AggregatedRating
        };

        // Upcoming This Week
        var today    = DateTime.UtcNow.Date;
        var weekEnd  = today.AddDays(7);
        var upcoming = await db.UpcomingGames
            .Where(u => u.ReleaseDate >= today && u.ReleaseDate <= weekEnd)
            .OrderBy(u => u.ReleaseDate)
            .Take(5)
            .Select(u => new UpcomingGameDto
            {
                IGDBId      = u.IGDBId,
                Name        = u.Name,
                CoverUrl    = u.CoverUrl,
                ReleaseDate = u.ReleaseDate
            })
            .ToListAsync();

        // Load games + journal once for both Backlog Summary and Quick Stats
        var games = await db.Games
            .Where(g => g.UserId == userId && g.IsActive)
            .Include(g => g.Genres)
            .ToListAsync();

        var journal = await db.GameJournalEntries
            .Where(j => j.UserId == userId)
            .ToListAsync();

        var gamesWithJournal = journal.Select(j => j.GameId).ToHashSet();
        var backlogCount     = journal.Count(j => j.Status == "Backlog");
        var completedCount   = journal.Count(j => j.Status == "Finished");
        var unplayedCount    = games.Count(g => !gamesWithJournal.Contains(g.Id));

        var completionDenominator = backlogCount + completedCount;
        var completionRate = completionDenominator > 0
            ? Math.Round((double)completedCount / completionDenominator * 100, 1)
            : 0;

        var backlogSummary = new BacklogSummaryDto
        {
            TotalBacklog   = backlogCount,
            Completed      = completedCount,
            CompletionRate = completionRate,
            Unplayed       = unplayedCount
        };

        // Quick Stats
        var totalPlaytime = games.Sum(g => g.PlaytimeHours);
        var mostPlayed    = games.MaxBy(g => g.PlaytimeHours);
        var favoriteGenre = games
            .SelectMany(g => g.Genres.Select(gr => gr.Name))
            .GroupBy(name => name)
            .OrderByDescending(grp => grp.Count())
            .Select(grp => grp.Key)
            .FirstOrDefault();

        var quickStats = new QuickStatsDto
        {
            TotalOwned         = games.Count,
            TotalPlaytimeHours = Math.Round(totalPlaytime, 1),
            FavoriteGenre      = favoriteGenre,
            MostPlayedGame     = mostPlayed?.PlaytimeHours > 0 ? mostPlayed.Name : null
        };

        return new DashboardDto
        {
            Username              = username,
            ContinuePlaying       = continuePlaying,
            TonightRecommendation = tonight,
            UpcomingThisWeek      = upcoming,
            BacklogSummary        = backlogSummary,
            QuickStats            = quickStats
        };
    }
}
