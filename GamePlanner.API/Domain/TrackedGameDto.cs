namespace GamePlanner.API.Domain;

public class TrackedGameDto
{
    public long     IGDBId      { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string?  CoverUrl    { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int      Hypes       { get; set; }
    public string[] Genres      { get; set; } = [];
    public int?     LocalGameId { get; set; }
    public DateTime TrackedAt   { get; set; }
}
