namespace GamePlanner.API.Entities;

public class GameJournalEntry
{
    public int       Id             { get; set; }
    public int       UserId         { get; set; }
    public int       GameId         { get; set; }
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

    public Game Game { get; set; } = null!;
}
