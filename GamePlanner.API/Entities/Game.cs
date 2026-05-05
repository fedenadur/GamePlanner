namespace GamePlanner.API.Entities;

public class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // IGDB fields
    public long? IGDBId { get; set; }

    // Steam fields
    public int? SteamAppId { get; set; }
    public double PlaytimeHours { get; set; }
    public string Source { get; set; } = "Manual";

    // Image fields
    public string? CoverImageUrl { get; set; }
    public DateTime? LastImageSyncDate { get; set; }

    // IGDB sync metadata
    public double?   AggregatedRating      { get; set; }
    public int?      AggregatedRatingCount  { get; set; }
    public DateTime? FirstReleaseDate       { get; set; }
    public DateTime? IGDBUpdatedAt          { get; set; }
    public DateTime? LastIGDBSyncAt         { get; set; }

    public int? UserId { get; set; }

    // Navigation
    public List<Genre>             Genres             { get; set; } = [];
    public List<Tag>               Tags               { get; set; } = [];
    public List<Theme>             Themes             { get; set; } = [];
    public List<PlayerPerspective> PlayerPerspectives { get; set; } = [];
    public List<GameMode>          GameModes          { get; set; } = [];
}
