namespace GamePlanner.API.Entities;

public class PlayerPerspective
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Game> Games { get; set; } = [];
}
