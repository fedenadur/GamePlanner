namespace GamePlanner.API.Entities;

public class GameImage
{
    public int    Id          { get; set; }
    public int    GameId      { get; set; }
    public string IgdbImageId { get; set; } = string.Empty;
    public string ImageType   { get; set; } = string.Empty;
    public string Url         { get; set; } = string.Empty;
    public int?   Width       { get; set; }
    public int?   Height      { get; set; }
    public DateTime SyncedAt  { get; set; }

    public Game Game { get; set; } = null!;
}
