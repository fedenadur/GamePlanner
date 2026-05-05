namespace GamePlanner.API.Domain;

public class CalendarGameDto
{
    public long     IGDBId      { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string?  CoverUrl    { get; set; }
    public int      Hypes       { get; set; }
    public int?     LocalGameId { get; set; }
    public bool     GenreMatch  { get; set; } // true if game genres overlap with user's library genres
    public bool     IsTracked   { get; set; }
    public string[] Genres      { get; set; } = [];
}
