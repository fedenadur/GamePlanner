namespace GamePlanner.API.Domain;

public static class PlaytimeCalculator
{
    /// <summary>Steam playtime wins when positive; otherwise the sum of logged sessions.</summary>
    public static double TotalHours(double steamHours, double loggedHours) =>
        steamHours > 0 ? steamHours : loggedHours;
}
