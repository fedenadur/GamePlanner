namespace GamePlanner.API.Domain;

public class DashboardDto
{
    public string?                   Username             { get; set; }
    public List<GameDto>             ContinuePlaying      { get; set; } = [];
    public TonightRecommendationDto? TonightRecommendation { get; set; }
    public List<UpcomingGameDto>     UpcomingThisWeek     { get; set; } = [];
    public BacklogSummaryDto         BacklogSummary       { get; set; } = new();
    public QuickStatsDto             QuickStats           { get; set; } = new();
}

public class TonightRecommendationDto
{
    public int     GameId        { get; set; }
    public string  Name          { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Reason        { get; set; }
    public double? MainStoryHours { get; set; }
    public double? AggregatedRating { get; set; }
}

public class UpcomingGameDto
{
    public long      IGDBId      { get; set; }
    public string    Name        { get; set; } = string.Empty;
    public string?   CoverUrl    { get; set; }
    public DateTime  ReleaseDate { get; set; }
}

public class BacklogSummaryDto
{
    public int    TotalBacklog   { get; set; }
    public int    Completed      { get; set; }
    public double CompletionRate { get; set; }
    public int    Unplayed       { get; set; }
}

public class QuickStatsDto
{
    public int     TotalOwned         { get; set; }
    public double  TotalPlaytimeHours { get; set; }
    public string? FavoriteGenre      { get; set; }
    public string? MostPlayedGame     { get; set; }
}
