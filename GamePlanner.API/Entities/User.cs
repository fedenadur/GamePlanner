namespace GamePlanner.API.Entities;

public class User
{
    public int     Id              { get; set; }
    public string? Email           { get; set; }
    public string? PasswordHash    { get; set; }
    public string? Username        { get; set; }
    public string? SteamId         { get; set; }
    public string? DisplayName     { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool    IsActive        { get; set; } = true;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    public List<Game> Games { get; set; } = [];
}
