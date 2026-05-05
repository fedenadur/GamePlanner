namespace GamePlanner.API.Domain;

public class SessionRequest
{
    public int AvailableMinutes { get; set; }  // 30, 60, or 120
    public string Mood { get; set; } = string.Empty;  // chill, focused, story, coop
}

public class SessionRecommendation
{
    public GameDto? Game { get; set; }
    public bool IsExactMatch { get; set; }
    public string Message { get; set; } = string.Empty;
}
