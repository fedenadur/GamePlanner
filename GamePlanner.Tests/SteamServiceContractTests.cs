using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GamePlanner.API.Services;

namespace GamePlanner.Tests;

/// <summary>
/// Steam credential transport and failure-surfacing contract. The key travels in the query string by
/// provider design, so redaction is a logging concern: the leak assertion must run through the real
/// IHttpClientFactory logging handlers (System.Net.Http.HttpClient.*), because that is where the key
/// leaked while every service-level catch block was already clean.
/// </summary>
public class SteamServiceContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string OwnedGamesJson =
        """{"response":{"game_count":1,"games":[{"appid":10,"name":"Counter-Strike","playtime_forever":120}]}}""";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (SteamService Service, RecordingHandler Handler, CapturingLoggerProvider Logs) Build()
    {
        var handler = new RecordingHandler();
        var logs = new CapturingLoggerProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Steam:ApiKey"] = ApiFactory.TestSteamKey })
            .Build();
        var logger = LoggerFactory.Create(b => b.AddProvider(logs)).CreateLogger<SteamService>();
        return (new SteamService(new StubHttpClientFactory(handler), config, logger), handler, logs);
    }

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    // ── Real pipeline ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOwnedGames_ThroughRealHttpClientPipeline_NeverLogsApiKey()
    {
        factory.Http.Reset();
        factory.Logs.Clear();
        factory.Http.Responder = _ => Json(OwnedGamesJson);

        var steam = factory.Services.GetRequiredService<SteamService>();
        await steam.GetOwnedGamesAsync("76561198000000001", CancellationToken.None);

        // (a) the key really was in play
        Assert.Contains(factory.Http.Requests, r => r.Uri.Query.Contains("key=" + ApiFactory.TestSteamKey));

        // (b) the capturing provider is wired into the real logging pipeline
        Assert.Contains(factory.Logs.Entries, e =>
            e.Category == "GamePlanner.API.Services.SteamService" && e.Level == LogLevel.Information);

        // (c) no log entry (message, exception, scope) carries the key
        var leaking = factory.Logs.Entries.Where(e => e.Text.Contains(ApiFactory.TestSteamKey)).ToList();
        Assert.True(leaking.Count == 0,
            "Steam key leaked into logs: " + string.Join(" | ",
                leaking.Select(e => $"{e.Category} [{e.Level}] {e.Text.Replace(ApiFactory.TestSteamKey, "<redacted>")}")));
    }

    // ── Transport ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOwnedGames_NumericSteamId_SendsSingleOwnedGamesRequestWithKeyOnce()
    {
        var (steam, handler, _) = Build();
        handler.Responder = _ => Json(OwnedGamesJson);

        var games = await steam.GetOwnedGamesAsync("76561198000000001", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/", request.Uri.GetLeftPart(UriPartial.Path));
        Assert.Equal(1, CountOf(request.Uri.Query, "key="));
        Assert.Contains("steamid=76561198000000001", request.Uri.Query);
        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath.Contains("ResolveVanityURL"));
        Assert.Single(games);
    }

    [Fact]
    public async Task GetOwnedGames_VanityInput_ResolvesVanityBeforeOwnedGames()
    {
        var (steam, handler, _) = Build();
        handler.Responder = r => r.RequestUri!.AbsolutePath.Contains("ResolveVanityURL")
            ? Json("""{"response":{"success":1,"steamid":"76561198000000002"}}""")
            : Json(OwnedGamesJson);

        await steam.GetOwnedGamesAsync("somevanity", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/ISteamUser/ResolveVanityURL/v1/", handler.Requests[0].Uri.AbsolutePath);
        Assert.Contains("vanityurl=somevanity", handler.Requests[0].Uri.Query);
        Assert.Equal("/IPlayerService/GetOwnedGames/v1/", handler.Requests[1].Uri.AbsolutePath);
        Assert.Contains("steamid=76561198000000002", handler.Requests[1].Uri.Query);
    }

    // ── Failure surfacing ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOwnedGames_Steam500_ThrowsHttpRequestException_WithoutLeakingKey()
    {
        var (steam, handler, logs) = Build();
        handler.Responder = _ => Json("{}", HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => steam.GetOwnedGamesAsync("76561198000000001", CancellationToken.None));

        Assert.DoesNotContain(ApiFactory.TestSteamKey, ex.ToString());
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, e => e.Text.Contains(ApiFactory.TestSteamKey));
    }

    [Fact]
    public async Task GetOwnedGames_NetworkFailure_IsRethrown_WithoutLeakingKey()
    {
        var (steam, handler, logs) = Build();
        handler.Responder = _ => throw new HttpRequestException("network down");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => steam.GetOwnedGamesAsync("76561198000000001", CancellationToken.None));

        Assert.Equal("network down", ex.Message);
        Assert.DoesNotContain(ApiFactory.TestSteamKey, ex.ToString());
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain(logs.Entries, e => e.Text.Contains(ApiFactory.TestSteamKey));
    }

    [Fact]
    public async Task GetOwnedGames_EmptyGamesList_ThrowsInvalidOperationException()
    {
        var (steam, handler, _) = Build();
        handler.Responder = _ => Json("""{"response":{"game_count":0}}""");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => steam.GetOwnedGamesAsync("76561198000000001", CancellationToken.None));
    }

    // ── Player summary ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerSummary_NonSuccessStatus_ReturnsNull()
    {
        var (steam, handler, _) = Build();
        handler.Responder = _ => Json("{}", HttpStatusCode.NotFound);

        var result = await steam.GetPlayerSummaryAsync("76561198000000001", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlayerSummary_HandlerThrows_RethrowsWithoutLeakingKey()
    {
        var (steam, handler, logs) = Build();
        handler.Responder = _ => throw new HttpRequestException("network down");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => steam.GetPlayerSummaryAsync("76561198000000001", CancellationToken.None));

        Assert.DoesNotContain(ApiFactory.TestSteamKey, ex.ToString());
        Assert.DoesNotContain(logs.Entries, e => e.Text.Contains(ApiFactory.TestSteamKey));
    }
}
