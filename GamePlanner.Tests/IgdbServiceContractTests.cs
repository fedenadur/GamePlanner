using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GamePlanner.API.Services;

namespace GamePlanner.Tests;

/// <summary>
/// Locks Phase 2 CRED-01 (client secret only in the token form body, never in a URL or header) and
/// REL-03 (IGDB failures surface as exceptions and are logged without credentials). Each fact builds
/// its own IgdbService because the token is cached per instance.
/// </summary>
public class IgdbServiceContractTests
{
    private const string AccessToken = "tok-123";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static bool IsTokenHost(HttpRequestMessage r) => r.RequestUri!.Host == "id.twitch.tv";

    private static (IgdbService Service, RecordingHandler Handler, CapturingLoggerProvider Logs) Build(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new RecordingHandler
        {
            Responder = responder ?? (r => IsTokenHost(r)
                ? Json($$"""{"access_token":"{{AccessToken}}","expires_in":3600}""")
                : Json("[]"))
        };
        var logs = new CapturingLoggerProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IGDB:ClientId"] = ApiFactory.TestIgdbClientId,
                ["IGDB:ClientSecret"] = ApiFactory.TestIgdbSecret
            })
            .Build();
        var logger = LoggerFactory.Create(b => b.AddProvider(logs)).CreateLogger<IgdbService>();
        return (new IgdbService(new StubHttpClientFactory(handler), config, logger), handler, logs);
    }

    [Fact]
    public async Task FirstQuery_PostsTokenRequestThenAuthorizedGamesQuery()
    {
        var (igdb, handler, _) = Build();

        await igdb.SearchMultipleAsync("Celeste", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);

        var token = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, token.Method);
        Assert.Equal("https://id.twitch.tv/oauth2/token", token.Uri.ToString());
        Assert.Equal(string.Empty, token.Uri.Query);
        Assert.Equal("application/x-www-form-urlencoded", token.ContentType);
        Assert.Contains("client_id=" + ApiFactory.TestIgdbClientId, token.Body);
        Assert.Contains("client_secret=" + ApiFactory.TestIgdbSecret, token.Body);
        Assert.Contains("grant_type=client_credentials", token.Body);

        var query = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, query.Method);
        Assert.Equal("https://api.igdb.com/v4/games", query.Uri.ToString());
        Assert.Equal("Bearer " + AccessToken, query.Headers["Authorization"]);
        Assert.Equal(ApiFactory.TestIgdbClientId, query.Headers["Client-ID"]);
        Assert.Equal("text/plain", query.ContentType);
        Assert.StartsWith("search \"Celeste\"", query.Body);
    }

    [Fact]
    public async Task ClientSecret_AppearsInNoRequestUriAndNoHeader()
    {
        var (igdb, handler, _) = Build();

        await igdb.SearchMultipleAsync("Celeste", CancellationToken.None);
        await igdb.FetchScreenshotsByGameIdsAsync([1942], 0, CancellationToken.None);

        Assert.NotEmpty(handler.Requests);
        foreach (var request in handler.Requests)
        {
            Assert.DoesNotContain(ApiFactory.TestIgdbSecret, request.Uri.ToString());
            Assert.All(request.Headers.Values, v => Assert.DoesNotContain(ApiFactory.TestIgdbSecret, v));
        }
    }

    [Fact]
    public async Task TwoQueries_OnOneInstance_IssueExactlyOneTokenRequest()
    {
        var (igdb, handler, _) = Build();

        await igdb.SearchMultipleAsync("Celeste", CancellationToken.None);
        await igdb.SearchMultipleAsync("Hades", CancellationToken.None);

        Assert.Single(handler.Requests, r => r.Uri.Host == "id.twitch.tv");
        Assert.Equal(2, handler.Requests.Count(r => r.Uri.Host == "api.igdb.com"));
    }

    [Fact]
    public async Task TokenEndpoint400_ThrowsHttpRequestException_AndNeverQueriesIgdb()
    {
        var (igdb, handler, _) = Build(r => IsTokenHost(r)
            ? Json("""{"message":"invalid client"}""", HttpStatusCode.BadRequest)
            : Json("[]"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => igdb.SearchMultipleAsync("Celeste", CancellationToken.None));

        Assert.DoesNotContain(handler.Requests, r => r.Uri.Host == "api.igdb.com");
    }

    [Fact]
    public async Task Query500_ThrowsHttpRequestException_LogsStatus_WithoutCredentials()
    {
        var (igdb, handler, logs) = Build(r => IsTokenHost(r)
            ? Json($$"""{"access_token":"{{AccessToken}}","expires_in":3600}""")
            : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => igdb.SearchMultipleAsync("Celeste", CancellationToken.None));

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Text.Contains("InternalServerError"));
        Assert.DoesNotContain(ApiFactory.TestIgdbSecret, ex.ToString());
        Assert.DoesNotContain(AccessToken, ex.ToString());
        Assert.DoesNotContain(logs.Entries, e => e.Text.Contains(ApiFactory.TestIgdbSecret));
        Assert.DoesNotContain(logs.Entries, e => e.Text.Contains(AccessToken));
    }

    [Fact]
    public async Task FetchScreenshots_PostsToScreenshotsEndpoint()
    {
        var (igdb, handler, _) = Build();

        await igdb.FetchScreenshotsByGameIdsAsync([1942], 0, CancellationToken.None);

        var query = Assert.Single(handler.Requests, r => r.Uri.Host == "api.igdb.com");
        Assert.Equal("https://api.igdb.com/v4/screenshots", query.Uri.ToString());
        Assert.Contains("where game = (1942)", query.Body);
        Assert.Contains("sort id asc;", query.Body);
        Assert.Contains("limit 500;", query.Body);
        Assert.Contains("offset 0;", query.Body);
    }

    [Fact]
    public async Task FetchArtworks_SendsTheRequestedOffset()
    {
        var (igdb, handler, _) = Build();

        await igdb.FetchArtworksByGameIdsAsync([1942], 500, CancellationToken.None);

        var query = Assert.Single(handler.Requests, r => r.Uri.Host == "api.igdb.com");
        Assert.Equal("https://api.igdb.com/v4/artworks", query.Uri.ToString());
        Assert.Contains("where game = (1942)", query.Body);
        Assert.Contains("offset 500;", query.Body);
    }

    [Fact]
    public async Task FetchExternalGamesBySteamAppId_QueriesSteamSourceWithQuotedUid()
    {
        var (igdb, handler, _) = Build();

        await igdb.FetchExternalGamesBySteamAppIdAsync(400, CancellationToken.None);

        var query = Assert.Single(handler.Requests, r => r.Uri.Host == "api.igdb.com");
        Assert.Equal(HttpMethod.Post, query.Method);
        Assert.Equal("https://api.igdb.com/v4/external_games", query.Uri.ToString());
        Assert.Contains("uid = \"400\"", query.Body);
        Assert.Contains("external_game_source = 1", query.Body);
        Assert.Contains("game != null", query.Body);
        Assert.Contains("sort game asc;", query.Body);
        Assert.Contains("limit 5;", query.Body);
        Assert.DoesNotContain("category", query.Body);
    }

    [Fact]
    public async Task FetchExternalGamesBySteamAppId_DeserializesStringUidAndGameId()
    {
        var (igdb, _, _) = Build(r => IsTokenHost(r)
            ? Json($$"""{"access_token":"{{AccessToken}}","expires_in":3600}""")
            : Json("""[{"id":1,"uid":"400","game":900},{"id":2,"uid":"400","game":71}]"""));

        var rows = await igdb.FetchExternalGamesBySteamAppIdAsync(400, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("400", r.Uid));
        Assert.Equal(new long?[] { 900, 71 }, rows.Select(r => r.Game));
    }

    [Fact]
    public async Task FetchExternalGamesBySteamAppId_ReturnsEmpty_WhenUnmapped()
    {
        var (igdb, _, _) = Build();

        var rows = await igdb.FetchExternalGamesBySteamAppIdAsync(400, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task SearchGame_AsksForTenResults()
    {
        var (igdb, handler, _) = Build();

        await igdb.SearchGameAsync("Portal", CancellationToken.None);

        var query = Assert.Single(handler.Requests, r => r.Uri.Host == "api.igdb.com");
        Assert.StartsWith("search \"Portal\"", query.Body);
        Assert.Contains("limit 10;", query.Body);
    }
}
