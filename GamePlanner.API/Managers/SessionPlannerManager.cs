using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using GamePlanner.API.Mapping;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class SessionPlannerManager(GamePlannerDbContext db)
{
    private static readonly Dictionary<string, string[]> MoodGenres = new()
    {
        ["chill"]   = ["Indie", "Simulation", "Puzzle", "Visual Novel"],
        ["focused"] = ["Action", "Platform", "Fighting", "Shooter", "Roguelike"],
        ["story"]   = ["RPG", "Adventure", "Role-playing (RPG)", "Point-and-click"],
        ["coop"]    = ["Co-operative", "Multiplayer", "Party"],
    };

    private static readonly string[] LongGenres  = ["RPG", "Role-playing (RPG)", "Strategy", "Simulation"];
    private static readonly string[] ShortGenres = ["Puzzle", "Indie", "Arcade", "Platform", "Fighting"];

    private static readonly Dictionary<string, string> PerspectiveMap = new()
    {
        ["firstperson"] = "first person",
        ["thirdperson"] = "third person",
        ["vr"]          = "virtual reality",
    };

    private static readonly Dictionary<string, string> GameModeMap = new()
    {
        ["solo"]        = "single player",
        ["coop"]        = "co-operative",
        ["multiplayer"] = "multiplayer",
    };

    public async Task<SessionPlannerResult> GetRecommendationsAsync(SessionPlannerRequest request, int userId, CancellationToken ct)
    {
        var games = await db.Games
            .Where(g => g.IsActive && g.UserId == userId)
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .ToListAsync(ct);

        games = request.PlayedStatus switch
        {
            "played"    => games.Where(g => g.PlaytimeHours > 0).ToList(),
            "notPlayed" => games.Where(g => g.PlaytimeHours == 0).ToList(),
            _           => games
        };

        if (games.Count == 0)
            return new SessionPlannerResult { Message = "No games found. Try a different filter." };

        // Load HLTB lookup once — keyed by SteamAppId and by Name
        var gameLength = request.GameLength.Trim().ToLower();
        Dictionary<int, GameHLTB>?    hltbBySteamId = null;
        Dictionary<string, GameHLTB>? hltbByName    = null;

        if (!string.IsNullOrEmpty(gameLength))
        {
            var allHltb = await db.GamesHLTB.ToListAsync(ct);
            hltbBySteamId = allHltb
                .Where(h => h.SteamAppId.HasValue)
                .GroupBy(h => h.SteamAppId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            hltbByName = allHltb
                .GroupBy(h => h.Name.ToLower())
                .ToDictionary(g => g.Key, g => g.First());
        }

        // Apply game-length filter — exclude games with no HLTB data when filter is active
        if (!string.IsNullOrEmpty(gameLength))
        {
            games = games
                .Select(g => (Game: g, Hltb: FindHltb(g, hltbBySteamId, hltbByName)))
                .Where(x => x.Hltb?.MainStoryHours is not null
                         && MatchesLength(x.Hltb.MainStoryHours!.Value, gameLength))
                .Select(x => x.Game)
                .ToList();

            if (games.Count == 0)
                return new SessionPlannerResult { Message = "No games match the selected game length." };
        }

        // Scoring
        var mood           = request.Mood.Trim().ToLower();
        var duration       = request.Duration.Trim().ToLower();
        var perspective    = request.Perspective.Trim().ToLower();
        var requestedModes = request.GameModes.Select(m => m.Trim().ToLower()).ToList();

        var moodKeywords = MoodGenres.TryGetValue(mood, out var kw)
            ? kw.Select(k => k.ToLower()).ToArray()
            : [];

        PerspectiveMap.TryGetValue(perspective, out var perspectiveIgdb);

        var modeIgdbValues = requestedModes
            .Select(m => GameModeMap.TryGetValue(m, out var v) ? v : null)
            .Where(v => v is not null)
            .ToList();

        var scored = games.Select(g =>
        {
            int score = 0;
            var genreNames       = g.Genres.Select(x => x.Name.ToLower()).ToList();
            var tagNames         = g.Tags.Select(x => x.Name.ToLower()).ToList();
            var themeNames       = g.Themes.Select(x => x.Name.ToLower()).ToList();
            var perspectiveNames = g.PlayerPerspectives.Select(x => x.Name.ToLower()).ToList();
            var modeNames        = g.GameModes.Select(x => x.Name.ToLower()).ToList();
            var allNames         = genreNames.Concat(tagNames).Concat(themeNames).ToList();

            // Mood: +2
            if (moodKeywords.Any(k => allNames.Any(n => n.Contains(k) || k.Contains(n))))
                score += 2;

            // Session duration: +2
            if (!string.IsNullOrEmpty(duration))
            {
                var inferred = InferDuration(g.EstimatedMinutes, genreNames, g.PlaytimeHours);
                if (inferred == duration) score += 2;
            }

            // Played boost: +1
            if (g.PlaytimeHours > 0) score += 1;

            // Rating boost: +2 highly rated (≥80), +1 well rated (≥60)
            //if (g.AggregatedRating >= 80) score += 2;
            //else if (g.AggregatedRating >= 60) score += 1;

            // Perspective: +1
            if (!string.IsNullOrEmpty(perspectiveIgdb))
            {
                if (perspective == "other")
                    score += perspectiveNames.Any(p =>
                        !p.Contains("first person") && !p.Contains("third person") && !p.Contains("virtual reality"))
                        ? 1 : 0;
                else if (perspectiveNames.Any(p => p.Contains(perspectiveIgdb!)))
                    score += 1;
            }

            // Game mode: +1 per match
            foreach (var mode in modeIgdbValues)
                if (modeNames.Any(m => m.Contains(mode!))) score += 1;

            // Game length: +2 if HLTB confirms the selected length
            if (!string.IsNullOrEmpty(gameLength))
            {
                var hltb = FindHltb(g, hltbBySteamId, hltbByName);
                if (hltb?.MainStoryHours is not null && MatchesLength(hltb.MainStoryHours.Value, gameLength))
                    score += 2;
            }

            return (Game: g, Score: score);
        })
        .OrderByDescending(x => x.Score)
        .ThenBy(_ => Random.Shared.Next())
        .Take(5)
        .ToList();

        return new SessionPlannerResult
        {
            Games   = scored.Select(x => x.Game.ToDto()).ToList(),
            Message = BuildMessage(mood, duration, request.PlayedStatus, perspective, requestedModes, gameLength)
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GameHLTB? FindHltb(
        Game game,
        Dictionary<int, GameHLTB>? bySteamId,
        Dictionary<string, GameHLTB>? byName)
    {
        if (bySteamId is null || byName is null) return null;
        if (game.SteamAppId.HasValue && bySteamId.TryGetValue(game.SteamAppId.Value, out var h)) return h;
        byName.TryGetValue(game.Name.ToLower(), out var hn);
        return hn;
    }

    private static bool MatchesLength(double hours, string filter) => filter switch
    {
        "short"  => hours <= 15,
        "medium" => hours > 15 && hours <= 30,
        "long"   => hours > 30,
        _        => true
    };

    private static string InferDuration(int estimatedMinutes, List<string> genres, double playtimeHours)
    {
        if (estimatedMinutes > 0)
            return estimatedMinutes switch { <= 30 => "short", <= 90 => "medium", _ => "long" };

        if (genres.Any(g => LongGenres.Any(l => g.Contains(l.ToLower()))))  return "long";
        if (genres.Any(g => ShortGenres.Any(s => g.Contains(s.ToLower())))) return "short";

        return playtimeHours > 50 ? "long" : "medium";
    }

    private static string BuildMessage(string mood, string duration, string playedStatus,
        string perspective, List<string> modes, string gameLength)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(duration))
            parts.Add(duration switch { "short" => "quick", "long" => "long", _ => "medium-length" });

        if (!string.IsNullOrEmpty(mood)) parts.Add(mood);

        if (!string.IsNullOrEmpty(perspective))
            parts.Add(perspective switch
            {
                "firstperson" => "first-person",
                "thirdperson" => "third-person",
                "vr"          => "VR",
                _             => perspective
            });

        if (modes.Count > 0) parts.Add(string.Join("/", modes));

        if (!string.IsNullOrEmpty(gameLength))
            parts.Add($"{gameLength}-game");

        var status = playedStatus switch
        {
            "played"    => " from your played games",
            "notPlayed" => " from your backlog",
            _           => ""
        };

        return parts.Count > 0
            ? $"Top picks for a {string.Join(", ", parts)} session{status}."
            : $"Top picks{status}.";
    }
}
