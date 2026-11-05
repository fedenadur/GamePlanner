namespace GamePlanner.API.Entities;

public class UserPreference
{
    public int    UserId       { get; set; }
    public int?   MaxGameHours { get; set; }
    public string SinglePlayer { get; set; } = "Neutral";
    public string Narrative    { get; set; } = "Neutral";
    public string Combat       { get; set; } = "Neutral";
}
