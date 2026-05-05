using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class StatsDashboardManager(GamePlannerDbContext db)
{
    public async Task<StatsDashboardDto> GetDashboardAsync(int userId)
    {
        // Load all user games with genres and journal entries in two queries
        var games = await db.Games
            .Where(g => g.UserId == userId)
            .Include(g => g.Genres)
            .ToListAsync();

        var journal = await db.GameJournalEntries
            .Where(j => j.UserId == userId)
            .Include(j => j.Game)
            .ToListAsync();

        var gameIds = games.Select(g => g.Id).ToHashSet();

        // Journal lookups
        var journalByGame  = journal.ToDictionary(j => j.GameId);
        var finishedEntries = journal.Where(j => j.Status == "Finished").ToList();
        var playingEntries  = journal.Where(j => j.Status == "Playing").ToList();
        var backlogEntries  = journal.Where(j => j.Status == "Backlog").ToList();

        // Games with no journal entry = never played
        var gamesWithJournal = journal.Select(j => j.GameId).ToHashSet();
        var neverPlayed = games.Count(g => !gamesWithJournal.Contains(g.Id));

        // Library
        var library = new LibraryStatsDto
        {
            TotalOwned       = games.Count,
            NeverPlayed      = neverPlayed,
            Finished         = finishedEntries.Count,
            CurrentlyPlaying = playingEntries.Count,
            Backlog          = backlogEntries.Count
        };

        // Time
        var totalHours    = games.Sum(g => g.PlaytimeHours);
        var gamesWithTime = games.Where(g => g.PlaytimeHours > 0).ToList();
        var avgHours      = gamesWithTime.Count > 0
            ? gamesWithTime.Average(g => g.PlaytimeHours)
            : 0;
        var mostPlayed    = games.MaxBy(g => g.PlaytimeHours);

        var time = new TimeStatsDto
        {
            TotalPlaytimeHours   = Math.Round(totalHours, 1),
            AveragePlaytimeHours = Math.Round(avgHours, 1),
            MostPlayedGame       = mostPlayed?.Name,
            MostPlayedGameHours  = Math.Round(mostPlayed?.PlaytimeHours ?? 0d, 1)
        };

        // Completion
        double completionRate = games.Count > 0
            ? Math.Round((double)finishedEntries.Count / games.Count * 100, 1)
            : 0;

        var completionDays = finishedEntries
            .Where(j => j.StartedAt.HasValue && j.FinishedAt.HasValue)
            .Select(j => (j.FinishedAt!.Value - j.StartedAt!.Value).TotalDays)
            .ToList();

        double? avgCompletionDays = completionDays.Count > 0
            ? Math.Round(completionDays.Average(), 1)
            : null;

        var completion = new CompletionStatsDto
        {
            CompletionRate       = completionRate,
            AverageCompletionDays = avgCompletionDays
        };

        // Ratings
        var rated = journal.Where(j => j.UserRating.HasValue).ToList();
        double? avgRating = rated.Count > 0
            ? Math.Round(rated.Average(j => (double)j.UserRating!.Value), 1)
            : null;

        var bestFinished = finishedEntries
            .Where(j => j.UserRating.HasValue)
            .MaxBy(j => j.UserRating!.Value);

        var ratings = new RatingStatsDto
        {
            AverageUserRating        = avgRating,
            HighestRatedFinishedGame = bestFinished?.Game.Name,
            HighestRating            = bestFinished?.UserRating
        };

        // Genres by count
        var allGenres = games.SelectMany(g => g.Genres.Select(gr => new { gr.Name, g.PlaytimeHours }));
        var genreGroups = allGenres
            .GroupBy(x => x.Name)
            .Select(g => new { Genre = g.Key, Count = g.Count(), Hours = g.Sum(x => x.PlaytimeHours) })
            .ToList();

        var genreStats = new GenreStatsDto
        {
            TopByCount = genreGroups
                .OrderByDescending(g => g.Count)
                .Take(5)
                .Select(g => new GenreCountDto { Genre = g.Genre, Count = g.Count })
                .ToList(),
            TopByHours = genreGroups
                .OrderByDescending(g => g.Hours)
                .Take(5)
                .Select(g => new GenreHoursDto { Genre = g.Genre, Hours = Math.Round(g.Hours, 1) })
                .ToList()
        };

        // By release year
        var byYear = games
            .Where(g => g.FirstReleaseDate.HasValue)
            .GroupBy(g => g.FirstReleaseDate!.Value.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearBucketDto { Year = g.Key, Count = g.Count() })
            .ToList();

        // Highlights
        var favoriteGenre = genreGroups.MaxBy(g => g.Count)?.Genre;

        // Hidden gem: finished, rating >= 8, playtime <= 10h
        var hiddenGem = finishedEntries
            .Where(j => j.UserRating >= 8 && j.Game.PlaytimeHours <= 10)
            .MaxBy(j => j.UserRating);

        // Oldest unplayed game
        var oldestUnplayed = games
            .Where(g => !gamesWithJournal.Contains(g.Id) && g.FirstReleaseDate.HasValue)
            .MinBy(g => g.FirstReleaseDate);

        var highlights = new HighlightsDto
        {
            MostPlayedGame     = mostPlayed?.Name,
            FavoriteGenre      = favoriteGenre,
            HiddenGem          = hiddenGem?.Game.Name,
            OldestUnplayedGame = oldestUnplayed?.Name,
            OldestUnplayedYear = oldestUnplayed?.FirstReleaseDate?.Year
        };

        return new StatsDashboardDto
        {
            Library    = library,
            Time       = time,
            Completion = completion,
            Ratings    = ratings,
            Genres     = genreStats,
            ByYear     = byYear,
            Highlights = highlights
        };
    }
}
