using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

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

    // ── Service-level rejection matrix (direct construction, no API host) ───

    private const string ValidId = "76561190000000001";

    private sealed record Rig(SteamAuthService Service, RecordingHandler Handler, CapturingLoggerProvider Logs, GamePlannerDbContext Db);

    private static Rig CreateRig(Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new RecordingHandler();
        if (responder is not null) handler.Responder = responder;

        var logs = new CapturingLoggerProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = ApiFactory.Secret,
                ["Jwt:Issuer"] = ApiFactory.Issuer,
                ["Jwt:Audience"] = ApiFactory.Audience,
                ["Steam:ApiKey"] = ApiFactory.TestSteamKey,
            })
            .Build();

        var db = new GamePlannerDbContext(new DbContextOptionsBuilder<GamePlannerDbContext>().UseSqlServer(ConnectionString).Options);
        var http = new StubHttpClientFactory(handler);
        var auth = new AuthService(db, config, NullLogger<AuthService>.Instance);
        var steam = new SteamService(http, config, NullLogger<SteamService>.Instance);

        return new Rig(new SteamAuthService(db, auth, steam, http, loggerFactory.CreateLogger<SteamAuthService>()), handler, logs, db);
    }

    private static QueryCollection ToQuery(Dictionary<string, string> fields) =>
        new(fields.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value)));

    private static HttpResponseMessage SteamReply(string body) =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };

    private static async Task AssertRejectedLocally(Dictionary<string, string> fields)
    {
        var rig = CreateRig(_ => SteamReply(ValidSteamReply));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(fields), CallbackUrl);

        Assert.Null(result);
        Assert.Empty(rig.Handler.Requests);
        Assert.Single(rig.Logs.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("Steam callback rejected"));
    }

    [Fact]
    public async Task Callback_SteamSaysInvalid_IsRejected_AfterOneVerificationRequestToSteam()
    {
        var rig = CreateRig(_ => SteamReply("ns:http://specs.openid.net/auth/2.0\nis_valid:false\n"));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(ValidQuery(ValidId, CallbackUrl)), CallbackUrl);

        Assert.Null(result);
        var sent = Assert.Single(rig.Handler.Requests);
        Assert.Equal("https://steamcommunity.com/openid/login", sent.Uri.ToString());
        Assert.Contains("openid.mode=check_authentication", sent.Body);
    }

    [Fact]
    public async Task Callback_VerificationRequestGoesToFixedEndpoint_WhateverTheCallerSupplies()
    {
        // op_endpoint and mode are checked locally, so an attacker-supplied endpoint never gets a request
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.op_endpoint"] = "https://evil.example/openid/login";
        var rig = CreateRig(_ => SteamReply(ValidSteamReply));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(fields), CallbackUrl);

        Assert.Null(result);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Callback_SteamAnswers500_IsRefused()
    {
        var rig = CreateRig(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(ValidQuery(ValidId, CallbackUrl)), CallbackUrl);

        Assert.Null(result);
        Assert.Single(rig.Handler.Requests);
    }

    [Fact]
    public async Task Callback_SteamCallThrows_IsRefused_NoExceptionEscapes()
    {
        var rig = CreateRig(_ => throw new HttpRequestException("simulated network failure"));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(ValidQuery(ValidId, CallbackUrl)), CallbackUrl);

        Assert.Null(result);
        Assert.Contains(rig.Logs.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("refusing login"));
    }

    [Theory]
    [InlineData("ns:x is_valid:true")]
    [InlineData("is_valid:true-not-really")]
    [InlineData("is_valid:false\nnote is_valid:true")]
    [InlineData("")]
    public async Task Callback_IsValidTrueOnlyInsideAnotherLine_IsRefused(string body)
    {
        // Fake id + cleanup: if this rule regressed, the assertion would be accepted and a user row created
        var steamId = FakeSteamId();
        var rig = CreateRig(_ => SteamReply(body));

        try
        {
            var result = await rig.Service.HandleCallbackAsync(ToQuery(ValidQuery(steamId, CallbackUrl)), CallbackUrl);

            Assert.Null(result);
        }
        finally
        {
            await DeleteUserBySteamIdAsync(steamId);
        }
    }

    [Fact]
    public async Task Callback_ForeignReturnTo_IsRejected_WithoutContactingSteam() =>
        await AssertRejectedLocally(ValidQuery(ValidId, "https://evil.example/api/auth/steam/callback"));

    [Fact]
    public async Task Callback_ReturnToDifferingOnlyInCase_IsRejected() =>
        await AssertRejectedLocally(ValidQuery(ValidId, CallbackUrl.ToUpperInvariant()));

    [Fact]
    public async Task Callback_ModeCancel_IsRejected()
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.mode"] = "cancel";
        await AssertRejectedLocally(fields);
    }

    [Fact]
    public async Task Callback_WrongNamespace_IsRejected()
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.ns"] = "http://specs.openid.net/auth/1.1";
        await AssertRejectedLocally(fields);
    }

    [Fact]
    public async Task Callback_ForeignOpEndpoint_IsRejected()
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.op_endpoint"] = "https://evil.example/openid/login";
        await AssertRejectedLocally(fields);
    }

    [Fact]
    public async Task Callback_SignedListMissingReturnTo_IsRejected_AndLogsTheReceivedList()
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.signed"] = "signed,op_endpoint,claimed_id,identity,response_nonce,assoc_handle";
        var rig = CreateRig(_ => SteamReply(ValidSteamReply));

        var result = await rig.Service.HandleCallbackAsync(ToQuery(fields), CallbackUrl);

        Assert.Null(result);
        Assert.Empty(rig.Handler.Requests);
        Assert.Contains(rig.Logs.Entries, e => e.Level == LogLevel.Warning
            && e.Text.Contains("signed,op_endpoint,claimed_id,identity,response_nonce,assoc_handle"));
    }

    [Fact]
    public async Task Callback_ForeignClaimedIdPrefix_IsRejected()
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.claimed_id"] = $"http://evil.example/openid/id/{ValidId}";
        await AssertRejectedLocally(fields);
    }

    [Theory]
    [InlineData("7656119800000000")]      // 16 digits
    [InlineData("765611980000000012")]    // 18 digits
    [InlineData("7656119800000000x")]     // 17 chars, non-digit
    [InlineData("76561198000000001/extra")] // trailing path segment
    public async Task Callback_MalformedSteamId_IsRejected(string steamId)
    {
        var fields = ValidQuery(ValidId, CallbackUrl);
        fields["openid.claimed_id"] = $"https://steamcommunity.com/openid/id/{steamId}";
        await AssertRejectedLocally(fields);
    }

    [Fact]
    public async Task Callback_RejectionLogs_NeverContainSigAssocHandleOrNonce()
    {
        var fields = ValidQuery(ValidId, "https://evil.example/cb");
        var rig = CreateRig(_ => SteamReply(ValidSteamReply));
        var failing = CreateRig(_ => throw new HttpRequestException("simulated"));

        await rig.Service.HandleCallbackAsync(ToQuery(fields), CallbackUrl);
        await failing.Service.HandleCallbackAsync(ToQuery(ValidQuery(ValidId, CallbackUrl)), CallbackUrl);

        foreach (var entry in rig.Logs.Entries.Concat(failing.Logs.Entries))
        {
            Assert.DoesNotContain(fields["openid.sig"], entry.Text);
            Assert.DoesNotContain(fields["openid.assoc_handle"], entry.Text);
            Assert.DoesNotContain(fields["openid.response_nonce"], entry.Text);
        }
    }

    [Fact]
    public async Task Callback_PlayerSummaryThrows_StillCreatesUserAndIssuesToken()
    {
        var steamId = FakeSteamId();
        var rig = CreateRig(req => req.RequestUri!.Host switch
        {
            "steamcommunity.com" => SteamReply(ValidSteamReply),
            _ => throw new HttpRequestException("simulated Steam Web API outage"),
        });

        try
        {
            var result = await rig.Service.HandleCallbackAsync(ToQuery(ValidQuery(steamId, CallbackUrl)), CallbackUrl);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result!.Token));
            var user = await rig.Db.Users.AsNoTracking().SingleAsync(u => u.SteamId == steamId);
            Assert.Null(user.DisplayName);
            Assert.Contains(rig.Logs.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("continuing without profile data"));
        }
        finally
        {
            await DeleteUserBySteamIdAsync(steamId);
        }
    }

    // ── Controller-level failure redirects ──────────────────────────────────

    [Fact]
    public async Task SteamCallback_SteamSaysInvalid_RedirectsToSteamFailed()
    {
        factory.Http.Reset();
        factory.Http.Responder = _ => SteamReply("ns:http://specs.openid.net/auth/2.0\nis_valid:false\n");

        var response = await factory.CreateApiClient()
            .GetAsync("/api/auth/steam/callback?" + ToQueryString(ValidQuery(ValidId, CallbackUrl)));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/login?error=steam_failed", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SteamCallback_SteamUnreachable_RedirectsToSteamFailed_NotA500()
    {
        factory.Http.Reset();
        factory.Http.Responder = _ => throw new HttpRequestException("simulated network failure");

        var response = await factory.CreateApiClient()
            .GetAsync("/api/auth/steam/callback?" + ToQueryString(ValidQuery(ValidId, CallbackUrl)));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/login?error=steam_failed", response.Headers.Location!.ToString());
    }
}
