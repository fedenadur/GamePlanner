namespace GamePlanner.API.Domain;

public static class PlaytimeCalculator
{
    /// <summary>Steam playtime wins when positive; otherwise the sum of logged sessions.</summary>
    public static double TotalHours(double steamHours, double loggedHours) =>
        steamHours > 0 ? steamHours : loggedHours;

    /// <summary>Never played = no total hours and still Backlog (no journal entry counts as Backlog).</summary>
    public static bool IsNeverPlayed(double totalHours, string? journalStatus) =>
        totalHours == 0 && journalStatus is null or JournalStatuses.Backlog;
}
