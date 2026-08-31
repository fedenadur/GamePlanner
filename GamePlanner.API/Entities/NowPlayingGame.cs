namespace GamePlanner.API.Entities;

public class NowPlayingGame
{
    public int      Id      { get; set; }
    public int      UserId  { get; set; }
    public int      GameId  { get; set; }
    public int      Slot    { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public Game Game { get; set; } = null!;
}
