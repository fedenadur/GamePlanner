namespace GamePlanner.API.Entities;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Game> Games { get; set; } = [];
}
