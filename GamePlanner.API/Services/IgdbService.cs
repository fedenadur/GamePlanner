using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GamePlanner.API.Domain;

namespace GamePlanner.API.Services;

public class IgdbService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<IgdbService> logger)
{
    private const string TokenUrl       = "https://id.twitch.tv/oauth2/token";
    private const string GamesUrl       = "https://api.igdb.com/v4/games";
    private const string ScreenshotsUrl = "https://api.igdb.com/v4/screenshots";
    private const string ArtworksUrl    = "https://api.igdb.com/v4/artworks";

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private string ClientId     => config["IGDB:ClientId"]!;
    private string ClientSecret => config["IGDB:ClientSecret"]!;
    public  int    BatchSize    => config.GetValue<int>("IGDB:BatchSize", 500);

    // ── Auth ────────────────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync()
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        var client = httpFactory.CreateClient();
        var url = $"{TokenUrl}?client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials";

        var response = await client.PostAsync(url, null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<IgdbTokenResponse>(json)!;

        _cachedToken = token.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60); // 1-min safety margin

        logger.LogInformation("IGDB token acquired, expires in {Seconds}s", token.ExpiresIn);
        return _cachedToken;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<IgdbGame>> FetchBatchAsync(int offset, CancellationToken ct)
    {
        var query = $"fields id,name,genres.name,first_release_date,updated_at; limit {BatchSize}; offset {offset}; sort id asc;";
        return await PostQueryAsync(query, ct);
    }

    public async Task<List<IgdbGame>> SearchMultipleAsync(string name, CancellationToken ct)
    {
        var escaped = name.Replace("\"", "\\\"");
        var query = $"search \"{escaped}\"; fields name,genres.name,cover.url,first_release_date; limit 5;";
        return await PostQueryAsync(query, ct);
    }

    public async Task<IgdbGame?> SearchGameAsync(string name, CancellationToken ct)
    {
        var escaped = name.Replace("\"", "\\\"");
        var query = $"search \"{escaped}\"; fields name,genres.name,themes.name,player_perspectives.name,game_modes.name,aggregated_rating,aggregated_rating_count,first_release_date,updated_at; limit 3;";
        var results = await PostQueryAsync(query, ct);
        return results.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? results.FirstOrDefault();
    }

    public async Task<IgdbGame?> FetchCoverByIdAsync(long igdbId, CancellationToken ct)
    {
        var query = $"fields name,cover.url,updated_at; where id = {igdbId}; limit 1;";
        var results = await PostQueryAsync(query, ct);
        return results.FirstOrDefault();
    }

    public async Task<List<IgdbGame>> FetchCoversByIdsAsync(IEnumerable<long> igdbIds, CancellationToken ct)
    {
        var ids   = string.Join(",", igdbIds);
        var query = $"fields id,name,cover.url,updated_at; where id = ({ids}); limit 50;";
        return await PostQueryAsync(query, ct);
    }

    public async Task<IgdbGame?> FetchEnrichmentByIdAsync(long igdbId, CancellationToken ct)
    {
        var query = $"fields name,genres.name,themes.name,player_perspectives.name,game_modes.name,aggregated_rating,aggregated_rating_count,first_release_date,updated_at; where id = {igdbId}; limit 1;";
        var results = await PostQueryAsync(query, ct);
        return results.FirstOrDefault();
    }

    public async Task<IgdbGame?> SearchCoverByNameAsync(string name, CancellationToken ct)
    {
        var escaped = name.Replace("\"", "\\\"");
        var query = $"search \"{escaped}\"; fields name,cover.url,updated_at; limit 3;";
        var results = await PostQueryAsync(query, ct);
        return results.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? results.FirstOrDefault();
    }

    public async Task<List<IgdbGame>> FetchUpcomingAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var from  = start.ToUnixTimeSeconds();
        var to    = end.ToUnixTimeSeconds();
        var query = $"fields id,name,cover.url,first_release_date,hypes,genres.name; where first_release_date >= {from} & first_release_date <= {to} & hypes > 0; sort hypes desc; limit 500;";
        return await PostQueryAsync(query, ct);
    }

    public async Task<List<IgdbImage>> FetchScreenshotsByGameIdsAsync(IEnumerable<long> igdbIds, CancellationToken ct)
    {
        var ids   = string.Join(",", igdbIds);
        var query = $"fields id,image_id,url,width,height,game; where game = ({ids}); limit 500;";
        return await PostQueryAsync<IgdbImage>(ScreenshotsUrl, query, ct);
    }

    public async Task<List<IgdbImage>> FetchArtworksByGameIdsAsync(IEnumerable<long> igdbIds, CancellationToken ct)
    {
        var ids   = string.Join(",", igdbIds);
        var query = $"fields id,image_id,url,width,height,game; where game = ({ids}); limit 500;";
        return await PostQueryAsync<IgdbImage>(ArtworksUrl, query, ct);
    }

    public static string OptimizeCoverUrl(string url) =>
        "https:" + url.Replace("t_thumb", "t_cover_big");

    public static string OptimizeImageUrl(string url) =>
        "https:" + url.Replace("t_thumb", "t_screenshot_big");

    public async Task<List<IgdbGame>> FetchUpdatedSinceAsync(DateTimeOffset since, int offset, CancellationToken ct)
    {
        var unixTimestamp = since.ToUnixTimeSeconds();
        var query = $"fields id,name,genres.name,first_release_date,updated_at; where updated_at > {unixTimestamp}; limit {BatchSize}; offset {offset}; sort updated_at asc;";
        return await PostQueryAsync(query, ct);
    }

    // ── Core HTTP ────────────────────────────────────────────────────────────

    private Task<List<IgdbGame>> PostQueryAsync(string query, CancellationToken ct) =>
        PostQueryAsync<IgdbGame>(GamesUrl, query, ct);

    private async Task<List<T>> PostQueryAsync<T>(string url, string query, CancellationToken ct)
    {
        var token  = await GetTokenAsync();
        var client = httpFactory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Client-ID", ClientId);

        var content  = new StringContent(query, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("IGDB error {Status}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<T>>(json) ?? [];
    }
}
