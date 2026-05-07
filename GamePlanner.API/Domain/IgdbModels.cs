using System.Text.Json.Serialization;

namespace GamePlanner.API.Domain;

public class IgdbGame
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<IgdbGenre>? Genres { get; set; }

    [JsonPropertyName("themes")]
    public List<IgdbTheme>? Themes { get; set; }

    [JsonPropertyName("player_perspectives")]
    public List<IgdbPlayerPerspective>? PlayerPerspectives { get; set; }

    [JsonPropertyName("game_modes")]
    public List<IgdbGameMode>? GameModes { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCover? Cover { get; set; }

    [JsonPropertyName("aggregated_rating")]
    public double? AggregatedRating { get; set; }

    [JsonPropertyName("aggregated_rating_count")]
    public int? AggregatedRatingCount { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; set; }   // Unix timestamp

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }            // Unix timestamp

    [JsonPropertyName("hypes")]
    public int? Hypes { get; set; }
}

public class IgdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IgdbTheme
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IgdbPlayerPerspective
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IgdbGameMode
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IgdbCover
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class IgdbImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("image_id")]
    public string ImageId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("game")]
    public long Game { get; set; }
}

public class IgdbTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
