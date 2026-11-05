namespace GamePlanner.API.Domain;

public class UserPreferencesDto
{
    public List<string> FavoriteGenres { get; set; } = [];
    public List<string> AvoidGenres    { get; set; } = [];
    public int?         MaxGameHours   { get; set; }
    public string       SinglePlayer   { get; set; } = "Neutral";
    public string       Narrative      { get; set; } = "Neutral";
    public string       Combat         { get; set; } = "Neutral";
}

public class SaveDevicesRequest
{
    public List<string> Devices { get; set; } = [];
}

public enum SaveDevicesResult { Ok, Invalid, DeviceInUse }
