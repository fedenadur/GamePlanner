namespace GamePlanner.API.Entities;

public class UpcomingGame
{
    public int       Id          { get; set; }
    public long      IGDBId      { get; set; }
    public string    Name        { get; set; } = string.Empty;
    public string?   CoverUrl    { get; set; }
    public DateTime  ReleaseDate { get; set; }
    public int       Hypes       { get; set; }
    public string?   Genres      { get; set; } // JSON array of strings
    public DateTime  CachedAt    { get; set; }
}
