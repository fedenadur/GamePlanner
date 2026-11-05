using Dapper;
using GamePlanner.API.Managers;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamePlanner.Tests;

/// <summary>
/// IMG-03: GameSyncManager matches an unmatched row through IGDB's Steam mapping, then exact name, then first hit.
/// Runs on GamePlannerDB_Test with a stubbed IGDB. Every probe row carries the "gsd-probe-sync-" prefix and is
/// purged before and after each fact. The plain arrange UPDATEs run under owner decision D-23 (test database only).
/// </summary>
[Collection(TestDatabase.CollectionName)]
public class GameSyncManagerMatcherTests
{
    private const string Prefix = "gsd-probe-sync-";
    private const string UserEmail = "gsd-probe-sync@test.local";

    private static long NewIgdbId() => Random.Shared.NextInt64(1_000_000_000, 2_000_000_000);

    private static int NewSteamAppId() => Random.Shared.Next(900_000_000, 999_999_999);

    private static string NewName(string tag) => $"{Prefix}{tag}-{Guid.NewGuid():N}";

    private static GameSyncManager CreateManager(IgdbService igdb) =>
        new(TestDatabase.CreateContext(), igdb, NullLogger<GameSyncManager>.Instance);

    private static async Task<int> NewProbeAsync(int userId, string name, long? igdbId = null, int? steamAppId = null, DateTime? igdbUpdatedAt = null)
    {
        var id = await TestDatabase.InsertProbeGameAsync(name, userId);
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Games SET IGDBId = @igdbId, SteamAppId = @steamAppId, Source = CASE WHEN @steamAppId IS NULL THEN Source ELSE 'Steam' END, IGDBUpdatedAt = @igdbUpdatedAt WHERE Id = @id",
            new { id, igdbId, steamAppId, igdbUpdatedAt });
        return id;
    }

    private static string GameJson(long id, string name, long updatedAt = 1700000000, bool full = true) =>
        full
            ? $$"""{"id":{{id}},"name":"{{name}}","first_release_date":1191974400,"updated_at":{{updatedAt}},"aggregated_rating":81.5,"aggregated_rating_count":12,"genres":[{"id":9,"name":"Puzzle"}],"themes":[{"id":17,"name":"Fantasy"}],"player_perspectives":[{"id":1,"name":"First person"}],"game_modes":[{"id":1,"name":"Single player"}]}"""
            : $$"""{"id":{{id}},"name":"{{name}}","updated_at":{{updatedAt}}}""";

    private static IEnumerable<RecordingHandler.Seen> IgdbRequests(RecordingHandler handler, string path) =>
        handler.Requests.Where(r => r.Uri.Host == "api.igdb.com" && r.Uri.AbsolutePath == path);

    private static async Task<(long? IgdbId, DateTime? Release)> ReadAsync(int gameId)
    {
        await using var db = TestDatabase.CreateContext();
        var g = await db.Games.AsNoTracking().FirstAsync(x => x.Id == gameId);
        return (g.IGDBId, g.FirstReleaseDate);
    }

    [Fact]
    public async Task SteamAppId_ResolvesThroughExternalGames()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var name = NewName("a");
            var gameId = await NewProbeAsync(userId, name, steamAppId: 400);

            var (igdb, handler) = IgdbStub.Create((path, body) =>
            {
                if (path == "/v4/external_games" && body.Contains("uid = \"400\""))
                    return """[{"id":15156,"uid":"400","game":71,"external_game_source":1}]""";
                if (path == "/v4/games" && body.Contains("where id = 71;"))
                    return $"[{GameJson(71, "Portal")}]";
                return "[]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await CreateManager(igdb).RunAsync(cts.Token);

            var (igdbId, release) = await ReadAsync(gameId);
            Assert.Equal(71, igdbId);
            Assert.Equal(new DateTime(2007, 10, 10), release!.Value.Date);

            var external = Assert.Single(IgdbRequests(handler, "/v4/external_games"), r => r.Body.Contains("uid = \"400\""));
            Assert.Contains("external_game_source = 1", external.Body);
            Assert.DoesNotContain(IgdbRequests(handler, "/v4/games"), r => r.Body.StartsWith("search \"") && r.Body.Contains(name));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task SteamAppId_SeveralMappings_UsesLowestGameId()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var name = NewName("g");
            var appId = NewSteamAppId();
            var low = NewIgdbId();
            var high = low + 5000;
            var gameId = await NewProbeAsync(userId, name, steamAppId: appId);

            var (igdb, handler) = IgdbStub.Create((path, body) =>
            {
                // Higher id first so the order of the response cannot decide.
                if (path == "/v4/external_games" && body.Contains($"uid = \"{appId}\""))
                    return $$"""[{"id":1,"uid":"{{appId}}","game":{{high}}},{"id":2,"uid":"{{appId}}","game":{{low}}}]""";
                if (path == "/v4/games" && body.Contains($"where id = {low};"))
                    return $"[{GameJson(low, "Lowest")}]";
                return "[]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await CreateManager(igdb).RunAsync(cts.Token);

            var (igdbId, _) = await ReadAsync(gameId);
            Assert.Equal(low, igdbId);
            Assert.DoesNotContain(IgdbRequests(handler, "/v4/games"), r => r.Body.Contains($"where id = {high};"));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }
}
