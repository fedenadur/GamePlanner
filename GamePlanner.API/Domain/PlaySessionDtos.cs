namespace GamePlanner.API.Domain;

public class PlaySessionDto
{
    public int      Id       { get; set; }
    public int      GameId   { get; set; }
    public string   Device   { get; set; } = string.Empty;
    public DateTime PlayedOn { get; set; }
    public decimal  Hours    { get; set; }
    public string?  Note     { get; set; }
}
