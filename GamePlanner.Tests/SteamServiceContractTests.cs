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
}
