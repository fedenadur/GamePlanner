using GamePlanner.API.Domain;

namespace GamePlanner.API.Managers.WhatToPlay;

public static class WhatToPlayScorer
{
    private const string Positive = "Positive";
    private const string Neutral  = "Neutral";
    private const string Negative = "Negative";

    private static readonly string[] BucketOrder = ["Short", "Medium", "Long"];

    public static List<WhatToPlayResultDto> Rank(
        IEnumerable<WhatToPlayCandidate> candidates, WhatToPlayRequest request, WhatToPlayProfile profile, int take = 5)
    {
        return candidates
            .Where(c => !IsExcluded(c, request, profile))
            .Select(c => Score(c, request, profile))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.GameId)
            .Take(take)
            .ToList();
    }

    private static bool IsExcluded(WhatToPlayCandidate c, WhatToPlayRequest request, WhatToPlayProfile profile)
    {
        if (c.Status is JournalStatuses.Finished or JournalStatuses.Dropped) return true;
        if (c.InNowPlaying) return true;

        bool anyDevice = string.IsNullOrEmpty(request.Device)
            || string.Equals(request.Device, DeviceTypes.General, StringComparison.OrdinalIgnoreCase);
        if (!anyDevice && c.PlayableDevices.Count > 0
            && !c.PlayableDevices.Contains(request.Device!, StringComparer.OrdinalIgnoreCase))
            return true;

        return c.Genres.Any(g => In(profile.AvoidGenres, g)
            && !request.Genres.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    private static WhatToPlayResultDto Score(WhatToPlayCandidate c, WhatToPlayRequest request, WhatToPlayProfile profile)
    {
        int score = 0;
        var reasons = new List<WhatToPlayReasonDto>();
        void Add(int points, string kind, string? text)
        {
            score += points;
            if (text is not null) reasons.Add(new WhatToPlayReasonDto { Kind = kind, Text = text });
        }

        // ── Genres ──────────────────────────────
        var requested = c.Genres.FirstOrDefault(g => request.Genres.Contains(g, StringComparer.OrdinalIgnoreCase));
        if (requested is not null) Add(30, Positive, $"{requested}, as requested");

        var favorite = c.Genres.FirstOrDefault(g => In(profile.FavoriteGenres, g));
        if (favorite is not null) Add(20, Positive, $"{favorite}, one of your favorite genres");
        else if (c.Genres.Any(g => In(profile.LikedGenres, g))) Add(10, Positive, "Similar to games you liked");

        if (c.Genres.Any(g => In(profile.DislikedGenres, g))) Add(-10, Negative, "Similar to games you didn't enjoy");

        // ── Length ──────────────────────────────
        if (c.LengthHours is null)
        {
            Add(0, Neutral, "No length data");
        }
        else
        {
            int h = RoundInt(c.LengthHours.Value);
            if (!string.Equals(request.GameLength, "Any", StringComparison.OrdinalIgnoreCase))
            {
                var bucket = GameKnowledge.LengthBucket(c.LengthHours)!;
                int actual = Array.FindIndex(BucketOrder, b => b == bucket);
                int wanted = Array.FindIndex(BucketOrder, b => string.Equals(b, request.GameLength, StringComparison.OrdinalIgnoreCase));
                if (wanted >= 0)
                {
                    if (actual == wanted) Add(20, Positive, $"~{h} h to beat, {BucketOrder[wanted].ToLowerInvariant()} as requested");
                    else Add(-15, Negative, $"~{h} h, {(actual < wanted ? "shorter" : "longer")} than requested");
                }
            }
            if (profile.MaxGameHours is int max && c.LengthHours.Value > max)
                Add(-20, Negative, $"~{h} h, above your {max} h limit");
        }

        // ── Session fit ─────────────────────────
        var fit = GameKnowledge.Fit(c.Genres, request.SessionTime);
        if (fit == SessionFit.Good)
            Add(15, Positive, $"Good fit for a {request.SessionTime.ToLowerInvariant()} session");
        else if (fit == SessionFit.Poor)
            Add(-10, Negative, string.Equals(request.SessionTime, "Short", StringComparison.OrdinalIgnoreCase)
                ? "Better for longer sessions" : "Better for short sessions");

        // ── Preferences ─────────────────────────
        Preference(profile.SinglePlayer, GameKnowledge.IsSinglePlayer(c.Modes), "Single-player", Add);
        Preference(profile.Narrative, GameKnowledge.IsNarrative(c.Genres, c.Themes), "Story-driven", Add);
        Preference(profile.Combat, GameKnowledge.IsCombat(c.Genres, c.Tags), "Combat-focused", Add);

        // ── Critics / state ─────────────────────
        if (c.CriticRating is double r)
        {
            int pts = (int)Math.Round(Math.Clamp((r - 70) / 2.0, -10, 15), MidpointRounding.AwayFromZero);
            Add(pts, Positive, r >= 80 ? $"Critics: {RoundInt(r)}" : null);
        }
        if (c.IsFavorite) Add(15, Positive, "In your favorites");
        if (c.Status == JournalStatuses.Paused) Add(5, Neutral, "Paused, pick up where you left off");
        if (c.TotalHours <= 0) Add(0, Neutral, "Never played");

        // ── Mood / perspective / modes ──────────
        if (!string.IsNullOrEmpty(request.Mood)
            && GameKnowledge.MoodGenres(request.Mood).Any(m => c.Genres.Contains(m, StringComparer.OrdinalIgnoreCase)))
            Add(10, Positive, $"Matches your {request.Mood.ToLowerInvariant()} mood");

        if (!string.IsNullOrEmpty(request.Perspective))
        {
            var p = c.Perspectives.FirstOrDefault(x => string.Equals(x, request.Perspective, StringComparison.OrdinalIgnoreCase));
            if (p is not null) Add(10, Positive, p);
        }
        foreach (var mode in request.Modes)
        {
            var m = c.Modes.FirstOrDefault(x => string.Equals(x, mode, StringComparison.OrdinalIgnoreCase));
            if (m is not null) Add(10, Positive, m);
        }

        // ── Strategy ────────────────────────────
        if (string.Equals(request.Strategy, "Resume", StringComparison.OrdinalIgnoreCase) && c.TotalHours > 0)
            Add(25, Positive, $"Already started ({RoundInt(c.TotalHours)} h)");

        if (string.Equals(request.Strategy, "Discover", StringComparison.OrdinalIgnoreCase) && c.Genres.Count > 0)
        {
            var main = c.Genres[0];
            int played = profile.GenrePlayCounts.FirstOrDefault(kv => string.Equals(kv.Key, main, StringComparison.OrdinalIgnoreCase)).Value;
            if (played <= 1) Add(25, Positive, "A genre you rarely play");
            if (In(profile.FavoriteGenres, main)) Add(-10, Neutral, null);
        }

        if (reasons.Count == 0) Add(0, Neutral, "Matches your filters");

        return new WhatToPlayResultDto
        {
            GameId = c.GameId, Name = c.Name, CoverImageUrl = c.CoverImageUrl, Store = c.Store,
            LengthHours = c.LengthHours, TotalHours = c.TotalHours, Score = score,
            Match = score >= 60 ? "High" : score >= 30 ? "Medium" : "Low",
            Reasons = reasons,
        };
    }

    private static void Preference(string pref, bool matches, string label, Action<int, string, string?> add)
    {
        if (!matches) return;
        if (string.Equals(pref, "Prefer", StringComparison.OrdinalIgnoreCase)) add(10, Positive, label);
        else if (string.Equals(pref, "Avoid", StringComparison.OrdinalIgnoreCase)) add(-15, Negative, $"Not your preference: {label}");
    }

    private static bool In(ISet<string> set, string value) =>
        set.Any(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    private static int RoundInt(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
}
