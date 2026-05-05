namespace GamePlanner.API.Domain;

public class HLTBGameDto
{
    public string  Name               { get; set; } = string.Empty;
    public int?    SteamAppId         { get; set; }
    public double? MainStoryHours     { get; set; }
    public double? MainPlusHours      { get; set; }
    public double? CompletionistHours { get; set; }
}
