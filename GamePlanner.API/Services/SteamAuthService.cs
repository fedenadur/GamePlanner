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
    IHttpClientFactory httpFactory,
    ILogger<SteamAuthService> logger)
{
    private const string SteamOpenIdUrl  = "https://steamcommunity.com/openid/login";
    private const string SteamIdPrefix   = "https://steamcommunity.com/openid/id/";
    private const string OpenIdNs        = "http://specs.openid.net/auth/2.0";

    // Fields the assertion signature must cover for us to trust them (OpenID 2.0 section 10.1)
    private static readonly string[] RequiredSignedFields =
        ["op_endpoint", "claimed_id", "identity", "return_to", "response_nonce"];

    public string BuildRedirectUrl(string callbackUrl)
    {
        var args = new Dictionary<string, string>
        {
            ["openid.ns"]         = OpenIdNs,
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

    public async Task<AuthResponse?> HandleCallbackAsync(IQueryCollection queryParams, string expectedReturnTo)
    {
        // 1. Validate with Steam
        var steamId = await ValidateAndExtractSteamIdAsync(queryParams, expectedReturnTo);
        if (steamId is null) return null;

        // 2. Fetch Steam profile (name + avatar)
        // Best-effort: a Steam Web API failure must not block login
        (string DisplayName, string AvatarUrl)? summary = null;
        try
        {
            summary = await steam.GetPlayerSummaryAsync(steamId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Steam player summary unavailable for {SteamId}; continuing without profile data.", steamId);
        }

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

    private async Task<string?> ValidateAndExtractSteamIdAsync(IQueryCollection query, string expectedReturnTo)
    {
        // Cheap local checks first; nothing below reaches Steam until all of them pass.
        if (query["openid.mode"].ToString() != "id_res")
            return Reject("openid.mode is not id_res");

        if (query["openid.ns"].ToString() != OpenIdNs)
            return Reject("openid.ns is not OpenID 2.0");

        if (query["openid.op_endpoint"].ToString() != SteamOpenIdUrl)
            return Reject("openid.op_endpoint is not the Steam endpoint");

        // The assertion must be addressed to this app's own callback (OpenID 2.0 section 11.1)
        if (!string.Equals(query["openid.return_to"].ToString(), expectedReturnTo, StringComparison.Ordinal))
            return Reject("return_to does not match this app's callback URL");

        var signed = query["openid.signed"].ToString();
        var signedFields = signed.Split(',', StringSplitOptions.TrimEntries);
        if (!RequiredSignedFields.All(f => signedFields.Contains(f, StringComparer.Ordinal)))
        {
            // Field names only (not secret); logged to diagnose a mismatch with Steam's real list
            var safeSigned = System.Text.RegularExpressions.Regex.IsMatch(signed, @"^[A-Za-z_.,]{0,200}$") ? signed : "<malformed>";
            logger.LogWarning("Steam callback rejected: {Reason}; received openid.signed={Signed}",
                "openid.signed does not cover the required fields", safeSigned);
            return null;
        }

        var claimedId = query["openid.claimed_id"].ToString();
        if (!claimedId.StartsWith(SteamIdPrefix, StringComparison.Ordinal))
            return Reject("claimed_id is not a Steam identity URL");

        var steamId = claimedId[SteamIdPrefix.Length..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, @"^\d{17}$"))
            return Reject("claimed_id does not end in a 17-digit SteamId");

        // Ask Steam to verify the signature; any failure refuses the login (never fails open)
        try
        {
            var args = query.ToDictionary(k => k.Key, v => v.Value.ToString());
            args["openid.mode"] = "check_authentication";

            var client   = httpFactory.CreateClient();
            var response = await client.PostAsync(SteamOpenIdUrl, new FormUrlEncodedContent(args));

            if (!response.IsSuccessStatusCode)
                return Reject("Steam check_authentication returned a non-success status");

            var body = await response.Content.ReadAsStringAsync();
            var valid = body.Split('\n').Any(line => string.Equals(line.Trim(), "is_valid:true", StringComparison.Ordinal));
            if (!valid)
                return Reject("Steam did not confirm the assertion");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Steam check_authentication failed; refusing login.");
            return null;
        }

        return steamId;
    }

    private string? Reject(string reason)
    {
        logger.LogWarning("Steam callback rejected: {Reason}", reason);
        return null;
    }

    private static string GetRealm(string callbackUrl)
    {
        var uri = new Uri(callbackUrl);
        return $"{uri.Scheme}://{uri.Authority}";
    }
}
