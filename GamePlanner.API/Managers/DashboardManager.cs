using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers.WhatToPlay;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class DashboardManager(
    GamePlannerDbContext db,
    NowPlayingManager nowPlayingManager,
    WhatToPlayManager whatToPlayManager)
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

        // Tonight Recommendation — top pick from What to Play with default filters
        var topPick = (await whatToPlayManager.RecommendAsync(userId, new WhatToPlayRequest(), 1, CancellationToken.None))
            .Results.FirstOrDefault();

        TonightRecommendationDto? tonight = topPick is null ? null : new TonightRecommendationDto
        {
            GameId           = topPick.GameId,
            Name             = topPick.Name,
            CoverImageUrl    = topPick.CoverImageUrl,
            Reason           = (topPick.Reasons.FirstOrDefault(r => r.Kind == "Positive") ?? topPick.Reasons.FirstOrDefault())?.Text,
            MainStoryHours   = topPick.LengthHours
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

        var statusByGame = await db.GameJournalEntries
            .Where(j => j.UserId == userId && j.Game.IsActive)
            .ToDictionaryAsync(j => j.GameId, j => j.Status);
        var sessions = await GameLibraryEnrichment.LoadSessionTotalsAsync(db, userId);
        double HoursOf(Entities.Game g) => PlaytimeCalculator.TotalHours(g.PlaytimeHours, sessions.GetValueOrDefault(g.Id).Hours);
        string? JournalStatusOf(Entities.Game g) => statusByGame.GetValueOrDefault(g.Id);

        // A game without a journal entry is Backlog
        var backlogCount   = games.Count(g => (JournalStatusOf(g) ?? JournalStatuses.Backlog) == JournalStatuses.Backlog);
        var completedCount = games.Count(g => JournalStatusOf(g) == JournalStatuses.Finished);
        var unplayedCount  = games.Count(g => PlaytimeCalculator.IsNeverPlayed(HoursOf(g), JournalStatusOf(g)));

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
        var totalPlaytime = games.Sum(HoursOf);
        var mostPlayed    = games.MaxBy(HoursOf);
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
            MostPlayedGame     = mostPlayed is not null && HoursOf(mostPlayed) > 0 ? mostPlayed.Name : null
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
