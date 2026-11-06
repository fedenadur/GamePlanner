namespace GamePlanner.API.Domain;

public class NowPlayingSlotDto
{
    public string               Device         { get; set; } = string.Empty;
    public GameDto?             Game           { get; set; }
    public DateTime?            PlayingSince   { get; set; }
    public List<PlaySessionDto> RecentSessions { get; set; } = [];
}

public class StartNowPlayingRequest
{
    public int     GameId          { get; set; }
    public string? OutgoingOutcome { get; set; }
    public byte?   OutgoingRating  { get; set; }
}

public class RemoveNowPlayingRequest
{
    public string Outcome { get; set; } = string.Empty;
    public byte?  Rating  { get; set; }
}

public enum NowPlayingResult
{
    Ok,
    GameNotFound,
    SlotOccupied,
    GameOnOtherDevice,
    InvalidOutcome,
    InvalidDevice,
    SlotEmpty
}
