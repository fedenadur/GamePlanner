namespace GamePlanner.API.Domain;

public class StatsDashboardDto
{
    public LibraryStatsDto       Library    { get; set; } = new();
    public TimeStatsDto          Time       { get; set; } = new();
    public CompletionStatsDto    Completion { get; set; } = new();
    public RatingStatsDto        Ratings    { get; set; } = new();
    public GenreStatsDto         Genres     { get; set; } = new();
    public List<YearBucketDto>   ByYear     { get; set; } = [];
    public HighlightsDto         Highlights { get; set; } = new();
}

public class LibraryStatsDto
{
    public int TotalOwned      { get; set; }
    public int NeverPlayed     { get; set; }
    public int Finished        { get; set; }
    public int CurrentlyPlaying { get; set; }
    public int Backlog         { get; set; }
}

public class TimeStatsDto
{
    public double TotalPlaytimeHours   { get; set; }
    public double AveragePlaytimeHours { get; set; }
    public string? MostPlayedGame      { get; set; }
    public double MostPlayedGameHours  { get; set; }
}

public class CompletionStatsDto
{
    public double CompletionRate        { get; set; } // % finished / owned
    public double? AverageCompletionDays { get; set; } // avg days from StartedAt to FinishedAt
}

public class RatingStatsDto
{
    public double? AverageUserRating        { get; set; }
    public string? HighestRatedFinishedGame { get; set; }
    public byte?   HighestRating            { get; set; }
}

public class GenreStatsDto
{
    public List<GenreCountDto> TopByCount { get; set; } = [];
    public List<GenreHoursDto> TopByHours { get; set; } = [];
}

public class GenreCountDto
{
    public string Genre { get; set; } = "";
    public int    Count { get; set; }
}

public class GenreHoursDto
{
    public string Genre { get; set; } = "";
    public double Hours { get; set; }
}

public class YearBucketDto
{
    public int Year  { get; set; }
    public int Count { get; set; }
}

public class HighlightsDto
{
    public string? MostPlayedGame     { get; set; }
    public string? FavoriteGenre      { get; set; }
    public string? HiddenGem          { get; set; } // high rating, low playtime
    public string? OldestUnplayedGame { get; set; }
    public int?    OldestUnplayedYear { get; set; }
}
