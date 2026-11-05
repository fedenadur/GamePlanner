namespace GamePlanner.API.Domain;

public static class DeviceTypes
{
    public const string PC        = "PC";
    public const string SteamDeck = "SteamDeck";
    public const string Switch    = "Switch";
    public const string PlayStation = "PlayStation";
    public const string Xbox      = "Xbox";
    public const string Mobile    = "Mobile";
    public const string Other     = "Other";
    public const string General   = "General";

    public static readonly IReadOnlyList<string> Selectable =
        [PC, SteamDeck, Switch, PlayStation, Xbox, Mobile, Other];

    public static bool IsSelectable(string device) => Selectable.Contains(device);
}

public static class Stores
{
    public const string Steam            = "Steam";
    public const string Epic             = "Epic";
    public const string GOG              = "GOG";
    public const string NintendoEShop    = "NintendoEShop";
    public const string PlayStationStore = "PlayStationStore";
    public const string XboxStore        = "XboxStore";
    public const string Physical         = "Physical";
    public const string Other            = "Other";

    public static readonly IReadOnlyList<string> All =
        [Steam, Epic, GOG, NintendoEShop, PlayStationStore, XboxStore, Physical, Other];

    /// <summary>Devices a store's games can run on. Empty list = any of the user's devices.</summary>
    public static IReadOnlyList<string> DefaultDevices(string store) => store switch
    {
        Steam            => [DeviceTypes.PC, DeviceTypes.SteamDeck],
        Epic or GOG      => [DeviceTypes.PC],
        NintendoEShop    => [DeviceTypes.Switch],
        PlayStationStore => [DeviceTypes.PlayStation],
        XboxStore        => [DeviceTypes.Xbox, DeviceTypes.PC],
        _                => []
    };
}

public static class JournalStatuses
{
    public const string Backlog   = "Backlog";
    public const string Playing   = "Playing";
    public const string Finished  = "Finished";
    public const string Paused    = "Paused";
    public const string Dropped   = "Dropped";
    public const string Replaying = "Replaying";

    public static bool IsActive(string status) => status is Playing or Replaying;
}
