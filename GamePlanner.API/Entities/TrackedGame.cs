namespace GamePlanner.API.Entities;

public class TrackedGame
{
    public int      Id        { get; set; }
    public int      UserId    { get; set; }
    public long     IGDBId    { get; set; }
    public DateTime TrackedAt { get; set; }
}
