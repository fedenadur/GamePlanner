using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers.WhatToPlay;

public class WhatToPlayManager(GamePlannerDbContext db, UserSettingsManager settings)
{
    private const int LikedRating = 8;
    private const int DislikedRating = 4;

    public async Task<WhatToPlayResponseDto> RecommendAsync(int userId, WhatToPlayRequest request, int take, CancellationToken ct)
    {
        var userDevices = await settings.GetDevicesAsync(userId);
        var device = ResolveDevice(request.Device, userDevices);

        var games = await db.Games.AsNoTracking().AsSplitQuery()
            .Where(g => g.IsActive && g.UserId == userId)
            .Include(g => g.Genres).Include(g => g.Themes).Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes).Include(g => g.Tags).Include(g => g.PlayableDevices)
            .ToListAsync(ct);
        var gameIds = games.Select(g => g.Id).ToList();

        var statuses = await GameLibraryEnrichment.LoadJournalStatusesAsync(db, userId, gameIds);
        var sessions = await GameLibraryEnrichment.LoadSessionTotalsAsync(db, userId, gameIds);
        var ratings = await db.GameJournalEntries.AsNoTracking()
            .Where(j => j.UserId == userId && j.UserRating != null)
            .ToDictionaryAsync(j => j.GameId, j => (int)j.UserRating!.Value, ct);
        var nowPlaying = (await db.NowPlayingGames.AsNoTracking()
            .Where(n => n.UserId == userId).Select(n => n.GameId).ToListAsync(ct)).ToHashSet();

        var hltb = await GameLibraryEnrichment.LoadHltbMainStoryAsync(db, games, ct);

        var candidates = new List<WhatToPlayCandidate>(games.Count);
        var likedGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dislikedGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var playCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in games)
        {
            var dto = GameLibraryEnrichment.ToLibraryDto(game, statuses, sessions, hltb);
            var genres = dto.Genres.Order(StringComparer.OrdinalIgnoreCase).ToList();

            var rating = ratings.GetValueOrDefault(game.Id);
            if (game.IsFavorite || rating >= LikedRating) likedGenres.UnionWith(genres);
            if (rating is > 0 and <= DislikedRating || dto.Status == JournalStatuses.Dropped) dislikedGenres.UnionWith(genres);
            if (dto.TotalHours > 0)
                foreach (var genre in genres) playCounts[genre] = playCounts.GetValueOrDefault(genre) + 1;

            candidates.Add(new WhatToPlayCandidate(
                game.Id, game.Name, game.CoverImageUrl, game.Store,
                dto.PlayableDevices, genres,
                dto.Themes, dto.PlayerPerspectives, dto.GameModes, dto.Tags,
                dto.Status, dto.TotalHours, dto.LengthHours, game.AggregatedRating,
                game.IsFavorite, nowPlaying.Contains(game.Id)));
        }

        var prefs = await settings.GetPreferencesAsync(userId);
        var profile = new WhatToPlayProfile(
            new HashSet<string>(prefs.FavoriteGenres, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(prefs.AvoidGenres, StringComparer.OrdinalIgnoreCase),
            prefs.MaxGameHours, prefs.SinglePlayer, prefs.Narrative, prefs.Combat,
            likedGenres, dislikedGenres, playCounts);

        var effective = new WhatToPlayRequest
        {
            Device = device, SessionTime = request.SessionTime, GameLength = request.GameLength,
            Genres = request.Genres, Mood = request.Mood, Perspective = request.Perspective,
            Modes = request.Modes, Strategy = request.Strategy
        };

        return new WhatToPlayResponseDto
        {
            Device = device,
            Results = WhatToPlayScorer.Rank(candidates, effective, profile, take)
        };
    }

    private static string ResolveDevice(string? requested, List<string> userDevices)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return userDevices.FirstOrDefault() ?? DeviceTypes.General;

        if (userDevices.Count == 0 && string.Equals(requested, DeviceTypes.General, StringComparison.OrdinalIgnoreCase))
            return DeviceTypes.General;

        return userDevices.FirstOrDefault(d => string.Equals(d, requested, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Device '{requested}' is not one of your devices.");
    }
}
