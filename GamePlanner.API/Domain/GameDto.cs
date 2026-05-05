namespace GamePlanner.API.Domain;

public class GameDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public List<string> Genres             { get; set; } = [];
    public List<string> Tags               { get; set; } = [];
    public List<string> Themes             { get; set; } = [];
    public List<string> PlayerPerspectives { get; set; } = [];
    public List<string> GameModes          { get; set; } = [];
    public string? CoverImageUrl { get; set; }
    public double PlaytimeHours { get; set; }
    public string Source { get; set; } = "Manual";
}

public class GameDetailDto : GameDto
{
    public int?    SteamAppId         { get; set; }
    public string? SteamUrl           { get; set; }
    public double? HltbMainStory           { get; set; }
    public double? HltbMainPlus            { get; set; }
    public double? HltbCompletionist       { get; set; }
    public double? AggregatedRating        { get; set; }
    public int?    AggregatedRatingCount   { get; set; }
}

public class CreateGameRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = [];
    public int EstimatedMinutes { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class UpdateGameRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = [];
    public int EstimatedMinutes { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class IgdbSearchResultDto
{
    public long IgdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public List<string> Genres { get; set; } = [];
    public int? ReleaseYear { get; set; }
}

public class CreateGameFromIgdbRequest
{
    public long IgdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public string? CoverUrl { get; set; }
    public List<string> Genres { get; set; } = [];
}
