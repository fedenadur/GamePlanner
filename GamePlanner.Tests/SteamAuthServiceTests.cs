using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace GamePlanner.Tests;

/// <summary>
/// Steam OpenID trust-boundary tests (D-09): S1 null-email token issuance, S2 return_to binding,
/// S3 fail-closed error handling, S4 strict assertion field checks. Facts that create a probe Steam
/// user touch the dev database (fake SteamId prefix 7656119, deleted in finally); the owner's row is never read.
/// </summary>
public class SteamAuthServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CallbackUrl = "https://localhost:7174/api/auth/steam/callback";
    private const string ValidSteamReply = "ns:http://specs.openid.net/auth/2.0\nis_valid:true\n";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;";

    private static string FakeSteamId()
    {
        var digits = string.Concat(Enumerable.Range(0, 10).Select(_ => Random.Shared.Next(0, 10)));
        return "7656119" + digits;
    }

    private static Dictionary<string, string> ValidQuery(string steamId, string returnTo) => new()
    {
        ["openid.ns"] = "http://specs.openid.net/auth/2.0",
        ["openid.mode"] = "id_res",
        ["openid.op_endpoint"] = "https://steamcommunity.com/openid/login",
        ["openid.claimed_id"] = $"https://steamcommunity.com/openid/id/{steamId}",
        ["openid.identity"] = $"https://steamcommunity.com/openid/id/{steamId}",
        ["openid.return_to"] = returnTo,
        ["openid.response_nonce"] = "2026-10-24T22:00:00Z-test-nonce-value",
        ["openid.assoc_handle"] = "1234567890-test-assoc-handle",
        ["openid.signed"] = "signed,op_endpoint,claimed_id,identity,return_to,response_nonce,assoc_handle",
        ["openid.sig"] = "dGVzdC1zaWduYXR1cmUtdmFsdWU=",
    };

    private static string ToQueryString(Dictionary<string, string> query) =>
        string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    private static async Task DeleteUserBySteamIdAsync(string steamId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM Users WHERE SteamId = @steamId", new { steamId });
    }

    private static AuthService CreateAuthService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = ApiFactory.Secret,
                ["Jwt:Issuer"] = ApiFactory.Issuer,
                ["Jwt:Audience"] = ApiFactory.Audience,
            })
            .Build();
        return new AuthService(null!, config, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);
    }

    // ── S1: first-time Steam login (user without email) ─────────────────────

    [Fact]
    public async Task SteamCallback_NewUserWithoutEmail_IssuesCodeThatExchangesForToken()
    {
        var steamId = FakeSteamId();
        factory.Http.Reset();
        factory.Http.Responder = req => req.RequestUri!.Host switch
        {
            "steamcommunity.com" => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(ValidSteamReply) },
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound),
        };

        try
        {
            var client = factory.CreateApiClient();
            var callback = await client.GetAsync("/api/auth/steam/callback?" + ToQueryString(ValidQuery(steamId, CallbackUrl)));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, callback.StatusCode);
            var location = callback.Headers.Location!.ToString();
            Assert.StartsWith("http://localhost:4200/login?code=", location);
            var code = location["http://localhost:4200/login?code=".Length..];

            var exchange = await client.PostAsJsonAsync("/api/auth/steam/exchange", new { code });
            Assert.Equal(System.Net.HttpStatusCode.OK, exchange.StatusCode);

            using var json = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(json.RootElement.GetProperty("token").GetString());

            Assert.Equal(steamId, jwt.Claims.Single(c => c.Type == "steamId").Value);
            Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Email || c.Type == "email");
        }
        finally
        {
            await DeleteUserBySteamIdAsync(steamId);
        }
    }

    [Fact]
    public void GenerateToken_UserWithoutEmail_OmitsEmailClaim_KeepsSteamId()
    {
        var user = new User { Id = 42, Email = null, SteamId = "76561190000000001" };

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(CreateAuthService().GenerateToken(user));

        Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid").Value);
        Assert.Equal("76561190000000001", jwt.Claims.Single(c => c.Type == "steamId").Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Email || c.Type == "email");
    }

    [Fact]
    public void GenerateToken_UserWithEmail_KeepsEmailClaim()
    {
        var user = new User { Id = 7, Email = "someone@example.test" };

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(CreateAuthService().GenerateToken(user));

        Assert.Contains(jwt.Claims, c => (c.Type == ClaimTypes.Email || c.Type == "email") && c.Value == "someone@example.test");
    }
}
