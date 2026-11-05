using System.Text.RegularExpressions;
using Dapper;
using GamePlanner.API.Managers;
using GamePlanner.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamePlanner.Tests;

/// <summary>
/// IMG-01 / IMG-02 behaviour of GameImageManager against GamePlannerDB_Test with a stubbed IGDB.
/// Every probe row carries the "gsd-probe-img-" name prefix and is purged before and after each fact.
/// The plain arrange UPDATEs run under owner decision D-23 (test database only).
/// </summary>
[Collection(TestDatabase.CollectionName)]
public class GameImageManagerTests
{
    private const string Prefix = "gsd-probe-img-";
    private const string UserEmail = "gsd-probe-img@test.local";
    private const string DummyCover = "https://x/t_cover_big/dummy.jpg";
    private static readonly long StubUpdatedAt = 1700000000;

    private static long NewIgdbId() => Random.Shared.NextInt64(1_000_000_000, 2_000_000_000);

    private static string NewName() => $"{Prefix}{Guid.NewGuid():N}";

    private static GameImageManager CreateManager(IgdbService igdb) =>
        new(TestDatabase.CreateContext(), igdb, TestDatabase.CreateConfig(), NullLogger<GameImageManager>.Instance);

    private static async Task ArrangeAsync(int gameId, long? igdbId, string? cover, DateTime? igdbUpdatedAt, DateTime? lastImageSync)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Games SET IGDBId = @igdbId, CoverImageUrl = @cover, IGDBUpdatedAt = @igdbUpdatedAt, LastImageSyncDate = @lastImageSync WHERE Id = @gameId",
            new { gameId, igdbId, cover, igdbUpdatedAt, lastImageSync });
    }

    private static async Task<int> NewProbeAsync(int userId, long? igdbId, string? cover, DateTime? igdbUpdatedAt, DateTime? lastImageSync, string? name = null)
    {
        var id = await TestDatabase.InsertProbeGameAsync(name ?? NewName(), userId);
        await ArrangeAsync(id, igdbId, cover, igdbUpdatedAt, lastImageSync);
        return id;
    }

    private static string CoverJson(long id, long updatedAt = 0) =>
        $$"""{"id":{{id}},"name":"stub","cover":{"id":1,"url":"//images.igdb.com/igdb/image/upload/t_thumb/co{{id}}.jpg"},"updated_at":{{(updatedAt == 0 ? StubUpdatedAt : updatedAt)}}}""";

    private static string ImageJson(string imageId, long game) =>
        $$"""{"id":1,"image_id":"{{imageId}}","url":"//images.igdb.com/igdb/image/upload/t_thumb/{{imageId}}.jpg","width":1920,"height":1080,"game":{{game}}}""";

    private static bool IsCoverByIdRequest(string path, string body) =>
        path == "/v4/games" && body.Contains("cover.url") && body.Contains("where id");

    private static List<long> RequestedIds(string body)
    {
        var m = Regex.Match(body, @"where id = \(([\d,]+)\)");
        return m.Success ? m.Groups[1].Value.Split(',').Select(long.Parse).ToList() : [];
    }

    [Fact]
    public async Task SharedIgdbId_SavesImagesForEveryLocalGame()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var x = NewIgdbId();
            var y = x + 1;
            var s1 = $"s1-{Guid.NewGuid():N}";
            var s2 = $"s2-{Guid.NewGuid():N}";
            var a1 = $"a1-{Guid.NewGuid():N}";

            var g1 = await NewProbeAsync(userId, x, DummyCover, null, null);
            var g2 = await NewProbeAsync(userId, x, DummyCover, null, null);
            var g3 = await NewProbeAsync(userId, y, DummyCover, null, null);

            var (igdb, _) = IgdbStub.Create((path, body) =>
            {
                if (!body.Contains(x.ToString())) return "[]";
                return path switch
                {
                    "/v4/screenshots" => $"[{ImageJson(s1, x)},{ImageJson(s2, x)}]",
                    "/v4/artworks" => $"[{ImageJson(a1, x)}]",
                    _ => "[]"
                };
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            foreach (var gid in new[] { g1, g2 })
            {
                var images = await db.GameImages.AsNoTracking().Where(i => i.GameId == gid).ToListAsync();
                Assert.Equal(new[] { s1, s2 }.OrderBy(v => v), images.Where(i => i.ImageType == "screenshot").Select(i => i.IgdbImageId).OrderBy(v => v));
                Assert.Equal(new[] { a1 }, images.Where(i => i.ImageType == "artwork").Select(i => i.IgdbImageId));
                Assert.All(images, i =>
                {
                    Assert.StartsWith("https:", i.Url);
                    Assert.Contains("t_screenshot_big", i.Url);
                });
            }

            Assert.Equal(0, await db.GameImages.CountAsync(i => i.GameId == g3));
            var games = await db.Games.AsNoTracking().Where(g => g.Id == g1 || g.Id == g2 || g.Id == g3).ToListAsync();
            Assert.All(games, g => Assert.NotNull(g.LastImageSyncDate));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }
}
