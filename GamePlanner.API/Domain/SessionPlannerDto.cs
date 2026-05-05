namespace GamePlanner.API.Domain;

public class SessionPlannerRequest
{
    public string PlayedStatus  { get; set; } = "all";        // all | played | notPlayed
    public string Mood          { get; set; } = string.Empty; // chill | focused | story | coop
    public string Duration      { get; set; } = string.Empty; // short | medium | long
    public string Perspective   { get; set; } = string.Empty; // firstPerson | thirdPerson | vr | other
    public List<string> GameModes  { get; set; } = [];         // solo | coop | multiplayer
    public string GameLength       { get; set; } = string.Empty; // short | medium | long
}

public class SessionPlannerResult
{
    public List<GameDto> Games   { get; set; } = [];
    public string        Message { get; set; } = string.Empty;
}
