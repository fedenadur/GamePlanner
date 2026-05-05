namespace GamePlanner.API.Entities;

public class GameHLTB
{
    public int     Id                 { get; set; }
    public string  Name               { get; set; } = string.Empty;
    public int?    SteamAppId         { get; set; }
    public double? MainStoryHours     { get; set; }
    public double? MainPlusHours      { get; set; }
    public double? CompletionistHours { get; set; }
    public DateTime LastUpdated       { get; set; } = DateTime.UtcNow;
}
