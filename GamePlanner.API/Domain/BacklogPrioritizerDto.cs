namespace GamePlanner.API.Domain;

public class BacklogPrioritizerResultDto
{
    public int     GameId         { get; set; }
    public string  Name           { get; set; } = string.Empty;
    public string? CoverImageUrl  { get; set; }
    public int     Score          { get; set; }
    public List<string> Reasons   { get; set; } = [];
    public double? MainStoryHours { get; set; }
    public double? AggregatedRating { get; set; }
    public double  PlaytimeHours  { get; set; }
    public List<string> Genres    { get; set; } = [];
}
