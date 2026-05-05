namespace GamePlanner.API.Domain;

public class GameJournalEntryDto
{
    public int       GameId         { get; set; }
    public string    GameName       { get; set; } = string.Empty;
    public string?   CoverImageUrl  { get; set; }
    public string    Status         { get; set; } = "Backlog";
    public DateTime? StartedAt      { get; set; }
    public DateTime? FinishedAt     { get; set; }
    public DateTime? LastPlayedAt   { get; set; }
    public byte?     UserRating     { get; set; }
    public string?   Notes          { get; set; }
    public string?   FavoriteMoment { get; set; }
    public int       ReplayCount    { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public DateTime  UpdatedAt      { get; set; }
    public int?      DaysToFinish   { get; set; }
}

public class UpsertJournalRequest
{
    public string    Status         { get; set; } = "Backlog";
    public DateTime? StartedAt      { get; set; }
    public DateTime? FinishedAt     { get; set; }
    public DateTime? LastPlayedAt   { get; set; }
    public byte?     UserRating     { get; set; }
    public string?   Notes          { get; set; }
    public string?   FavoriteMoment { get; set; }
    public int       ReplayCount    { get; set; }
}
