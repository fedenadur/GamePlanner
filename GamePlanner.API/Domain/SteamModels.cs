using System.Text.Json.Serialization;

namespace GamePlanner.API.Domain;

// ── Steam API response models ────────────────────────────────────────────────

public class SteamOwnedGamesResponse
{
    [JsonPropertyName("response")]
    public SteamOwnedGamesPayload? Response { get; set; }
}

public class SteamOwnedGamesPayload
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamGame>? Games { get; set; }
}

public class SteamGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeMinutes { get; set; }
}

public class SteamVanityResponse
{
    [JsonPropertyName("response")]
    public SteamVanityPayload? Response { get; set; }
}

public class SteamVanityPayload
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("success")]
    public int Success { get; set; }
}

// ── Player Summary ───────────────────────────────────────────────────────────

public class SteamPlayerSummaryResponse
{
    [JsonPropertyName("response")]
    public SteamPlayerSummaryPayload? Response { get; set; }
}

public class SteamPlayerSummaryPayload
{
    [JsonPropertyName("players")]
    public List<SteamPlayer>? Players { get; set; }
}

public class SteamPlayer
{
    [JsonPropertyName("personaname")]
    public string PersonaName { get; set; } = string.Empty;

    [JsonPropertyName("avatarfull")]
    public string AvatarFull { get; set; } = string.Empty;
}

// ── Clean DTO returned to the manager ───────────────────────────────────────

public class SteamGameDto
{
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double PlaytimeHours { get; set; }
}
