namespace GamePlanner.API.Domain;

public static class GameLength
{
    // sp_InsertSteamGame stores this placeholder; it says nothing about the real length.
    public const int SteamPlaceholderMinutes = 60;

    /// <summary>HLTB main story when known; otherwise EstimatedMinutes / 60. A Steam placeholder without HLTB is unknown (null).</summary>
    public static double? Hours(int? steamAppId, int estimatedMinutes, double? hltbMainStory)
    {
        if (hltbMainStory is > 0) return hltbMainStory;
        if (estimatedMinutes <= 0) return null;
        if (steamAppId is not null && estimatedMinutes == SteamPlaceholderMinutes) return null;
        return estimatedMinutes / 60.0;
    }
}
