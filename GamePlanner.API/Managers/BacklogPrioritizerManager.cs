using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class BacklogPrioritizerManager(GamePlannerDbContext db)
{
    public async Task<List<BacklogPrioritizerResultDto>> PrioritizeAsync(
        int userId, string? mode, int limit = 10)
    {
        // Load user games (active, not in Now Playing)
        var nowPlayingIds = await db.NowPlayingGames
            .Where(n => n.UserId == userId)
            .Select(n => n.GameId)
            .ToListAsync();

        var games = await db.Games
            .Where(g => g.IsActive && g.UserId == userId && !nowPlayingIds.Contains(g.Id))
            .Include(g => g.Genres)
            .ToListAsync();

        // Load HLTB lookup (by SteamAppId + by name)
        var steamIds = games.Where(g => g.SteamAppId.HasValue).Select(g => g.SteamAppId!.Value).ToList();
        var gameNames = games.Select(g => g.Name.ToLower()).ToList();

        var hltbRecords = await db.GamesHLTB
            .Where(h => (h.SteamAppId.HasValue && steamIds.Contains(h.SteamAppId.Value))
                     || gameNames.Contains(h.Name.ToLower()))
            .ToListAsync();

        // Detect favorite genre (most frequent across all user games)
        var favoriteGenre = await db.Games
            .Where(g => g.IsActive && g.UserId == userId)
            .Include(g => g.Genres)
            .SelectMany(g => g.Genres.Select(gr => gr.Name))
            .GroupBy(name => name)
            .OrderByDescending(grp => grp.Count())
            .Select(grp => grp.Key)
            .FirstOrDefaultAsync();

        var normalizedMode = mode?.ToLower() ?? "";
        var isFreshStart = normalizedMode == "freshstart";
        var isComfortZone = normalizedMode == "comfortzone";
        var isSmartPick = string.IsNullOrEmpty(mode);

        var results = new List<BacklogPrioritizerResultDto>();

        foreach (var game in games)
        {
            // Smart Pick: focus on resuming barely-started games
            if (isSmartPick && game.PlaytimeHours >= 2) continue;

            // Comfort Zone: only unplayed games from favorite genre
            if (isComfortZone && game.PlaytimeHours > 0) continue;
            if (isComfortZone && !string.IsNullOrEmpty(favoriteGenre))
            {
                var matchesFavorite = game.Genres.Any(g => g.Name.Equals(favoriteGenre, StringComparison.OrdinalIgnoreCase));
                if (!matchesFavorite) continue;
            }

            // Fresh Start: only unplayed games, but exclude favorite genre (discovery)
            if (isFreshStart && game.PlaytimeHours > 0) continue;
            if (isFreshStart && !string.IsNullOrEmpty(favoriteGenre))
            {
                var matchesFavorite = game.Genres.Any(g => g.Name.Equals(favoriteGenre, StringComparison.OrdinalIgnoreCase));
                if (matchesFavorite) continue; // Skip favorite genre to encourage discovery
            }

            // Resolve HLTB by SteamAppId first, then name fallback
            var hltb = game.SteamAppId.HasValue
                ? hltbRecords.FirstOrDefault(h => h.SteamAppId == game.SteamAppId)
                : null;
            hltb ??= hltbRecords.FirstOrDefault(h =>
                h.Name.Equals(game.Name, StringComparison.OrdinalIgnoreCase));

            var genres = game.Genres.Select(g => g.Name).ToList();
            var (score, reasons) = CalculateScore(game, hltb?.MainStoryHours, favoriteGenre, genres, mode);

            results.Add(new BacklogPrioritizerResultDto
            {
                GameId          = game.Id,
                Name            = game.Name,
                CoverImageUrl   = game.CoverImageUrl,
                Score           = score,
                Reasons         = reasons,
                MainStoryHours  = hltb?.MainStoryHours,
                AggregatedRating = game.AggregatedRating,
                PlaytimeHours   = game.PlaytimeHours,
                Genres          = genres,
            });
        }

        return results.OrderByDescending(r => r.Score).Take(limit).ToList();
    }

    private static (int Score, List<string> Reasons) CalculateScore(
        Entities.Game game,
        double? mainStoryHours,
        string? favoriteGenre,
        List<string> genres,
        string? mode)
    {
        var score = 0;
        var reasons = new List<string>();
        var normalizedMode = mode?.ToLower() ?? "";

        // 1. Never Played / Resumption Status
        if (normalizedMode == "smartpick")
        {
            // Smart Pick: boost games you've started (resumption focus)
            if (game.PlaytimeHours > 0)
            {
                score += 60;
                reasons.Add("Time to resume");
            }
        }
        else if (game.PlaytimeHours == 0)
        {
            score += (normalizedMode == "freshstart") ? 50 : 30;
            reasons.Add("Never played");
        }
        else if (game.PlaytimeHours < 2)
        {
            score += (normalizedMode == "freshstart") ? 0 : 15;
            reasons.Add("Barely started");
        }

        // 2. Rating Quality
        if (game.AggregatedRating.HasValue)
        {
            var r = game.AggregatedRating.Value;
            if (normalizedMode == "prestige")
            {
                // Prestige mode: rating dominates everything
                if (r >= 90)      { score += 80; reasons.Add("Excellent reviews"); }
                else if (r >= 85) { score += 60; reasons.Add("Outstanding rating"); }
                else if (r >= 80) { score += 40; reasons.Add("Very well reviewed"); }
                else if (r >= 75) { score += 15; reasons.Add("Well reviewed"); }
                else              { score -= 50; }
            }
            else if (normalizedMode == "hiddengems")
            {
                // Hidden Gems: care about rating but less dominantly
                if (r >= 75)      { score += 30; reasons.Add("Strong rating"); }
                else if (r >= 70) { score += 15; reasons.Add("Good rating"); }
                else              { score -= 40; }
            }
            else
            {
                // Default/other modes: balanced rating approach
                if (r >= 90)      { score += 25; reasons.Add("Excellent reviews"); }
                else if (r >= 80) { score += 18; reasons.Add("Very well reviewed"); }
                else if (r >= 70) { score += 10; reasons.Add("Well reviewed"); }
                else              { score += 3; }
            }

            // Don't penalize few reviews in Prestige or Hidden Gems (they're about finding quality)
            if (game.AggregatedRatingCount.HasValue && game.AggregatedRatingCount < 20 && normalizedMode != "prestige" && normalizedMode != "hiddengems")
            {
                score -= 5;
                reasons.Add("Few reviews — less reliable score");
            }
        }
        else if (normalizedMode == "prestige")
        {
            // Prestige mode heavily penalizes unrated games
            score -= 60;
        }

        // 3. Length / Finishability
        if (mainStoryHours.HasValue)
        {
            var h = mainStoryHours.Value;
            if (normalizedMode == "quickwins")
            {
                // Quick Wins: length is the primary factor
                if (h <= 5)       { score += 100; reasons.Add("Ultra-quick finish"); }
                else if (h <= 10) { score += 80; reasons.Add("Perfect for a quick win"); }
                else if (h <= 15) { score += 40; reasons.Add("Reasonably short"); }
                else if (h <= 25) { score += 10; }
                else              { score -= 60; }
            }
            else if (normalizedMode == "hiddengems" || normalizedMode == "prestige")
            {
                // Hidden Gems & Prestige: ignore length (quality > playtime)
                // Don't apply length bonuses/penalties
            }
            else if (normalizedMode == "comfortzone" || normalizedMode == "freshstart")
            {
                // These modes care somewhat about length
                if (h <= 10)      { score += 15; reasons.Add("Short — easy to finish"); }
                else if (h <= 20) { score += 10; reasons.Add("Medium length"); }
                else if (h <= 35) { score += 5; reasons.Add("Moderate length"); }
            }
            else
            {
                // Smart Pick: balanced length scoring
                if (h <= 10)      { score += 20; reasons.Add("Short — easy to finish"); }
                else if (h <= 20) { score += 15; reasons.Add("Medium length"); }
                else if (h <= 35) { score += 10; reasons.Add("Moderate length"); }
                else              { score += 4; }

                if (h > 60) { score -= 10; reasons.Add("Very long commitment"); }
            }
        }
        else if (normalizedMode == "quickwins")
        {
            // Quick Wins: unknown length is risky
            score -= 40;
            reasons.Add("Unknown length — risky for quick wins");
        }

        // 4. Recency Bonus
        if (game.FirstReleaseDate.HasValue)
        {
            var age = (DateTime.UtcNow - game.FirstReleaseDate.Value).TotalDays / 365.25;
            if (normalizedMode != "prestige") // Prestige doesn't care about recency
            {
                if (age <= 2)       { score += 10; reasons.Add("Recent release"); }
                else if (age <= 5)  { score += 5; reasons.Add("New-ish"); }
                else if (age >= 15) { score += 3; reasons.Add("Classic title"); }
            }
        }

        // 5. Genre Preference
        if (normalizedMode == "comfortzone")
        {
            // Comfort Zone: already filtered to favorite genre only, so give big bonus
            if (!string.IsNullOrEmpty(favoriteGenre))
            {
                score += 80;
                reasons.Add($"Perfect match for your favorite genre ({favoriteGenre})");
            }
        }
        else if (normalizedMode == "freshstart")
        {
            // Fresh Start: no genre bonus (already filtered to exclude favorite)
            // Encourage discovery of other genres
        }
        else if (normalizedMode != "prestige" && normalizedMode != "quickwins")
        {
            // Smart Pick & other modes: moderate genre bonus
            var matchesFavorite = !string.IsNullOrEmpty(favoriteGenre) &&
                genres.Any(g => g.Equals(favoriteGenre, StringComparison.OrdinalIgnoreCase));

            if (matchesFavorite)
            {
                score += 15;
                reasons.Add($"Matches your favorite genre ({favoriteGenre})");
            }
        }

        // 6. Hidden Gems specific: obscurity factor
        if (normalizedMode == "hiddengems")
        {
            var ratingCount = game.AggregatedRatingCount ?? 0;
            if (ratingCount is > 0 and < 100)
            {
                score += 50;
                reasons.Add("Hidden gem — under the radar");
            }
            else if (ratingCount is >= 100 and < 300)
            {
                score += 20;
            }
            else if (ratingCount >= 500)
            {
                score -= 50; // too mainstream
            }
        }

        score = Math.Max(0, score);
        return (score, reasons);
    }
}
