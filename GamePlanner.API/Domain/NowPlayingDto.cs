namespace GamePlanner.API.Domain;

public class NowPlayingSlotDto
{
    public int      Slot { get; set; }
    public GameDto? Game { get; set; }
}

public class SetNowPlayingRequest
{
    public int GameId { get; set; }
}
