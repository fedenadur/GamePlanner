using System.Text.Json;
using GamePlanner.API.Domain;

namespace GamePlanner.API.Services;

public class SteamService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<SteamService> logger)
{
    private const string BaseUrl = "https://api.steampowered.com";

    private string ApiKey => config["Steam:ApiKey"]!;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Accepts either a numeric SteamID64 or a vanity URL name.
    /// Returns the user's owned games as clean DTOs.
    /// </summary>
    public async Task<List<SteamGameDto>> GetOwnedGamesAsync(string steamInput, CancellationToken ct)
    {
        var steamId = await ResolveSteamIdAsync(steamInput, ct);
        return await FetchOwnedGamesAsync(steamId, ct);
    }

    // ── Resolution ───────────────────────────────────────────────────────────

    private async Task<string> ResolveSteamIdAsync(string input, CancellationToken ct)
    {
        // Numeric SteamID64 — use directly
        if (input.All(char.IsDigit) && input.Length >= 15)
            return input;

        // Vanity URL — could be the full URL or just the name
        var vanity = input
            .TrimEnd('/')
            .Split('/')
            .Last();

        logger.LogInformation("Resolving Steam vanity URL: {Vanity}", vanity);

        var client = httpFactory.CreateClient();
        var url = $"{BaseUrl}/ISteamUser/ResolveVanityURL/v1/?key={ApiKey}&vanityurl={vanity}";

        try
        {
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<SteamVanityResponse>(json);

            if (result?.Response?.Success != 1 || result.Response.SteamId is null)
                throw new InvalidOperationException($"Could not resolve Steam vanity URL: \"{vanity}\". Make sure the profile is public.");

            return result.Response.SteamId;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Steam vanity resolution failed for '{Vanity}'.", vanity);
            throw;
        }
    }

    // ── Player Summary ───────────────────────────────────────────────────────

    public async Task<(string DisplayName, string AvatarUrl)?> GetPlayerSummaryAsync(string steamId, CancellationToken ct)
    {
        var client   = httpFactory.CreateClient();
        var url      = $"{BaseUrl}/ISteamUser/GetPlayerSummaries/v2/?key={ApiKey}&steamids={steamId}";

        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<SteamPlayerSummaryResponse>(json);
            var player = result?.Response?.Players?.FirstOrDefault();
            if (player is null) return null;

            return (player.PersonaName, player.AvatarFull);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Steam player summary lookup failed for SteamID {SteamId}.", steamId);
            throw;
        }
    }

    // ── Owned Games ──────────────────────────────────────────────────────────

    private async Task<List<SteamGameDto>> FetchOwnedGamesAsync(string steamId, CancellationToken ct)
    {
        var client = httpFactory.CreateClient();
        var url = $"{BaseUrl}/IPlayerService/GetOwnedGames/v1/?key={ApiKey}&steamid={steamId}&include_appinfo=true&format=json";

        logger.LogInformation("Fetching owned games for SteamID: {SteamId}", steamId);

        try
        {
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<SteamOwnedGamesResponse>(json);

            var games = result?.Response?.Games;
            if (games is null || games.Count == 0)
                throw new InvalidOperationException("No games found. The Steam profile may be private or the library is empty.");

            return games.Select(g => new SteamGameDto
            {
                AppId        = g.AppId,
                Name         = g.Name,
                PlaytimeHours = Math.Round(g.PlaytimeMinutes / 60.0, 1)
            }).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fetching owned games failed for SteamID {SteamId}.", steamId);
            throw;
        }
    }
}
