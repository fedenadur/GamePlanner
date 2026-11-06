namespace GamePlanner.API.Managers.WhatToPlay;

public enum SessionFit { Good, Neutral, Poor }

public static class GameKnowledge
{
    private static readonly Dictionary<string, string[]> MoodGenreMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chill"]   = ["Indie", "Simulation", "Puzzle", "Visual Novel"],
        ["focused"] = ["Action", "Platform", "Fighting", "Shooter", "Roguelike"],
        ["story"]   = ["RPG", "Adventure", "Role-playing (RPG)", "Point-and-click"],
        ["coop"]    = ["Co-operative", "Multiplayer", "Party"],
    };

    private static readonly string[] LongGenres  = ["RPG", "Role-playing (RPG)", "Strategy", "Simulation"];
    private static readonly string[] ShortGenres = ["Puzzle", "Indie", "Arcade", "Platform", "Fighting"];

    private static readonly string[] NarrativeGenres =
        ["Adventure", "Role-playing (RPG)", "RPG", "Visual Novel", "Point-and-click"];
    private static readonly string[] CombatGenres = ["Shooter", "Fighting", "Hack and slash/Beat 'em up"];

    public static SessionFit Fit(IEnumerable<string> genres, string sessionTime)
    {
        var list = genres.ToList();
        bool isShort = list.Any(g => ContainsAny(g, ShortGenres));
        bool isLong  = list.Any(g => ContainsAny(g, LongGenres));

        if (string.Equals(sessionTime, "Short", StringComparison.OrdinalIgnoreCase))
            return isShort ? SessionFit.Good : isLong ? SessionFit.Poor : SessionFit.Neutral;

        if (string.Equals(sessionTime, "Long", StringComparison.OrdinalIgnoreCase))
            return isLong ? SessionFit.Good : SessionFit.Neutral;

        return SessionFit.Neutral;
    }

    public static bool IsNarrative(IEnumerable<string> genres, IEnumerable<string> themes) =>
        genres.Any(g => EqualsAny(g, NarrativeGenres)) || themes.Any(t => EqualsAny(t, ["Drama"]));

    public static bool IsCombat(IEnumerable<string> genres, IEnumerable<string> tags) =>
        genres.Any(g => EqualsAny(g, CombatGenres)) || tags.Any(t => EqualsAny(t, ["Action"]));

    public static bool IsSinglePlayer(IEnumerable<string> modes) =>
        modes.Any(m => EqualsAny(m, ["Single player"]));

    public static string[] MoodGenres(string mood) =>
        MoodGenreMap.TryGetValue(mood, out var genres) ? genres : [];

    public static string? LengthBucket(double? hours) => hours switch
    {
        null => null,
        < 10 => "Short",
        <= 30 => "Medium",
        _ => "Long",
    };

    private static bool ContainsAny(string value, string[] names) =>
        names.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool EqualsAny(string value, string[] names) =>
        names.Any(n => string.Equals(value, n, StringComparison.OrdinalIgnoreCase));
}
