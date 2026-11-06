namespace GamePlanner.API.Entities;

public class PlaySession
{
    public int       Id         { get; set; }
    public int       UserId     { get; set; }
    public int       GameId     { get; set; }
    public string    Device     { get; set; } = string.Empty;
    public DateTime  PlayedOn   { get; set; }
    public decimal   Hours      { get; set; }
    public string?   Note       { get; set; }
    public DateTime  CreatedAt  { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
