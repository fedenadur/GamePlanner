using System.Web;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Services;

public class SteamAuthService(
    GamePlannerDbContext db,
    AuthService auth,
    SteamService steam,
    IHttpClientFactory httpFactory)
{
    private const string SteamOpenIdUrl  = "https://steamcommunity.com/openid/login";
    private const string SteamIdPrefix   = "https://steamcommunity.com/openid/id/";

    public string BuildRedirectUrl(string callbackUrl)
    {
        var args = new Dictionary<string, string>
        {
            ["openid.ns"]         = "http://specs.openid.net/auth/2.0",
            ["openid.mode"]       = "checkid_setup",
            ["openid.return_to"]  = callbackUrl,
            ["openid.realm"]      = GetRealm(callbackUrl),
            ["openid.identity"]   = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };

        var query = string.Join("&", args.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{SteamOpenIdUrl}?{query}";
    }

    public async Task<AuthResponse?> HandleCallbackAsync(IQueryCollection queryParams)
    {
        // 1. Validate with Steam
        var steamId = await ValidateAndExtractSteamIdAsync(queryParams);
        if (steamId is null) return null;

        // 2. Fetch Steam profile (name + avatar)
        var summary = await steam.GetPlayerSummaryAsync(steamId, CancellationToken.None);

        // 3. Find or create user
        var user = await db.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);

        if (user is null)
        {
            user = new User
            {
                SteamId         = steamId,
                DisplayName     = summary?.DisplayName,
                ProfileImageUrl = summary?.AvatarUrl,
                Username        = summary?.DisplayName ?? $"steam_{steamId[^6..]}"
            };
            db.Users.Add(user);
        }
        else if (!user.IsActive)
        {
            return null;
        }
        else
        {
            // Refresh profile on every login
            user.DisplayName     = summary?.DisplayName ?? user.DisplayName;
            user.ProfileImageUrl = summary?.AvatarUrl   ?? user.ProfileImageUrl;
        }

        await db.SaveChangesAsync();

        return new AuthResponse
        {
            Token    = auth.GenerateToken(user),
            Email    = user.Email,
            Username = user.DisplayName ?? user.Username
        };
    }

    private async Task<string?> ValidateAndExtractSteamIdAsync(IQueryCollection query)
    {
        // Build validation request (same params but mode = check_authentication)
        var args = query.ToDictionary(k => k.Key, v => v.Value.ToString());
        args["openid.mode"] = "check_authentication";

        var content  = new FormUrlEncodedContent(args);
        var client   = httpFactory.CreateClient();
        var response = await client.PostAsync(SteamOpenIdUrl, content);

        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("is_valid:true")) return null;

        // Extract and validate SteamId from claimed_id
        var claimedId = query["openid.claimed_id"].ToString();
        if (!claimedId.StartsWith(SteamIdPrefix)) return null;

        var steamId = claimedId[SteamIdPrefix.Length..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, @"^\d{17}$")) return null;

        return steamId;
    }

    private static string GetRealm(string callbackUrl)
    {
        var uri = new Uri(callbackUrl);
        return $"{uri.Scheme}://{uri.Authority}";
    }
}
