namespace GamePlanner.API.Entities;

public class UserPreferenceGenre
{
    public int    UserId  { get; set; }
    public int    GenreId { get; set; }
    public string Kind    { get; set; } = string.Empty;
}
