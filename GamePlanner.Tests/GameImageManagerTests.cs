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

    [Fact]
    public async Task NullCover_WithIgdbUpdatedAtSet_GetsCover_EvenWhenIgdbTimestampIsNotNewer()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var older = NewIgdbId();
            var equal = older + 1;
            var igdbUpdated = DateTimeOffset.FromUnixTimeSeconds(StubUpdatedAt).UtcDateTime;
            var recent = DateTime.UtcNow;

            // IGDB updated_at (1700000000) is older than the stored value here...
            var gOlder = await NewProbeAsync(userId, older, null, new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc), recent);
            // ...and exactly equal to it here.
            var gEqual = await NewProbeAsync(userId, equal, null, igdbUpdated, recent);

            var (igdb, _) = IgdbStub.Create((path, body) =>
            {
                if (!IsCoverByIdRequest(path, body)) return "[]";
                var ids = RequestedIds(body).Where(i => i == older || i == equal).ToList();
                return "[" + string.Join(",", ids.Select(i => CoverJson(i))) + "]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            foreach (var gid in new[] { gOlder, gEqual })
            {
                var g = await db.Games.AsNoTracking().SingleAsync(x => x.Id == gid);
                Assert.StartsWith("https:", g.CoverImageUrl);
                Assert.Contains("t_cover_big", g.CoverImageUrl);
                Assert.Equal(igdbUpdated, g.IGDBUpdatedAt);
            }
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task RowWithCover_IsNeverRequestedForACover()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var z = NewIgdbId();
            var gid = await NewProbeAsync(userId, z, DummyCover, null, DateTime.UtcNow);

            var (igdb, handler) = IgdbStub.Create((_, _) => "[]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath == "/v4/games" && r.Body.Contains(z.ToString()));
            await using var db = TestDatabase.CreateContext();
            Assert.Equal(DummyCover, (await db.Games.AsNoTracking().SingleAsync(g => g.Id == gid)).CoverImageUrl);
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task CoverLoop_RequestsEachRowOnce_WhenIgdbHasNoCovers()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);

            // Strays (active NULL-cover rows outside this fact's prefix) are deactivated first (test arrange, D-23).
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
            {
                await conn.ExecuteAsync("UPDATE Games SET IsActive = 0 WHERE IsActive = 1 AND CoverImageUrl IS NULL AND Name NOT LIKE @p", new { p = Prefix + "%" });
                Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM Games WHERE IsActive = 1 AND CoverImageUrl IS NULL AND Name NOT LIKE @p", new { p = Prefix + "%" }));
            }

            var probeIds = new List<long>();
            var baseId = NewIgdbId();
            for (var i = 0; i < 120; i++)
            {
                var igdbId = baseId + i;
                probeIds.Add(igdbId);
                await NewProbeAsync(userId, igdbId, null, null, DateTime.UtcNow);
            }

            var (igdb, handler) = IgdbStub.Create((_, _) => "[]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            var probeSet = probeIds.ToHashSet();
            var coverRequests = handler.Requests
                .Where(r => IsCoverByIdRequest(r.Uri.AbsolutePath, r.Body) && RequestedIds(r.Body).Any(probeSet.Contains))
                .Select(r => RequestedIds(r.Body))
                .ToList();

            Assert.Equal(3, coverRequests.Count);
            Assert.Equal(probeIds.Count, coverRequests.SelectMany(c => c).Count());
            for (var k = 0; k < 3; k++)
                Assert.Equal(probeIds.Skip(k * 50).Take(50), coverRequests[k]);
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task NewlyCoveredRow_GetsScreenshotsInTheSameRun()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var x = NewIgdbId();
            var gid = await NewProbeAsync(userId, x, null, null, null);
            var s1 = $"s1-{Guid.NewGuid():N}";
            var s2 = $"s2-{Guid.NewGuid():N}";

            var (igdb, _) = IgdbStub.Create((path, body) =>
            {
                if (!body.Contains(x.ToString())) return "[]";
                if (IsCoverByIdRequest(path, body)) return "[" + CoverJson(x) + "]";
                return path == "/v4/screenshots" ? $"[{ImageJson(s1, x)},{ImageJson(s2, x)}]" : "[]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            var g = await db.Games.AsNoTracking().SingleAsync(v => v.Id == gid);
            Assert.Contains("t_cover_big", g.CoverImageUrl);
            Assert.Equal(2, await db.GameImages.CountAsync(i => i.GameId == gid && i.ImageType == "screenshot"));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task CoverBatchFailure_RetriesEachRowOnItsOwn()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var p = NewIgdbId();
            var q = p + 1;
            var nameQ = NewName();
            var gp = await NewProbeAsync(userId, p, null, null, DateTime.UtcNow);
            var gq = await NewProbeAsync(userId, q, null, null, DateTime.UtcNow, nameQ);

            var (igdb, handler) = IgdbStub.Create((path, body) =>
            {
                if (!IsCoverByIdRequest(path, body)) return "[]";
                var ids = RequestedIds(body);
                var hasP = ids.Contains(p);
                var hasQ = ids.Contains(q);
                if (hasP && hasQ) return null;
                if (hasP) return "[" + CoverJson(p) + "]";
                if (hasQ) return null;
                return "[]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var (failedCount, failureDetail) = await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            Assert.Contains("t_cover_big", (await db.Games.AsNoTracking().SingleAsync(g => g.Id == gp)).CoverImageUrl);
            Assert.Null((await db.Games.AsNoTracking().SingleAsync(g => g.Id == gq)).CoverImageUrl);

            var refs = handler.Requests.Count(r =>
                IsCoverByIdRequest(r.Uri.AbsolutePath, r.Body) && RequestedIds(r.Body).Any(i => i == p || i == q));
            Assert.Equal(3, refs);
            Assert.Equal(1, failedCount);
            Assert.Contains(nameQ, failureDetail);
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task NullCoverWithoutIgdbId_UsesNameSearch_AndGetsCover()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var name = NewName();
            // Stored IGDBUpdatedAt (2030) is newer than the IGDB updated_at (1700000000): D-01 must still save the cover.
            var gid = await NewProbeAsync(userId, null, null, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, name);

            var (igdb, handler) = IgdbStub.Create((path, body) =>
                path == "/v4/games" && body.StartsWith("search \"") && body.Contains(name)
                    ? $$"""[{"id":1,"name":"{{name}}","cover":{"id":1,"url":"//images.igdb.com/igdb/image/upload/t_thumb/conamesearch.jpg"},"updated_at":{{StubUpdatedAt}}}]"""
                    : "[]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            var g = await db.Games.AsNoTracking().SingleAsync(v => v.Id == gid);
            Assert.Contains("t_cover_big", g.CoverImageUrl);
            Assert.Null(g.IGDBId);
            Assert.Equal(1, handler.Requests.Count(r => r.Body.StartsWith("search \"") && r.Body.Contains(name)));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }

    [Fact]
    public async Task ImageRichBatch_PagesScreenshotsUntilAShortPage()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var userId = await TestDatabase.EnsureUserAsync(UserEmail);
            var x = NewIgdbId();
            var gameId = await NewProbeAsync(userId, x, DummyCover, null, null);
            var tag = Guid.NewGuid().ToString("N")[..8];

            string Page(int from, int count) =>
                "[" + string.Join(",", Enumerable.Range(from, count).Select(i => ImageJson($"ss{tag}{i:D4}", x))) + "]";

            var (igdb, handler) = IgdbStub.Create((path, body) =>
            {
                if (!body.Contains(x.ToString())) return "[]";
                if (path == "/v4/screenshots")
                    return body.Contains("offset 500;") ? Page(500, 3) : body.Contains("offset 0;") ? Page(0, 500) : "[]";
                return "[]";
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await CreateManager(igdb).RunAsync(cts.Token);

            await using var db = TestDatabase.CreateContext();
            Assert.Equal(503, await db.GameImages.CountAsync(i => i.GameId == gameId && i.ImageType == "screenshot"));

            var screenshotRequests = handler.Requests
                .Where(r => r.Uri.AbsolutePath == "/v4/screenshots" && r.Body.Contains(x.ToString())).ToList();
            Assert.Equal(2, screenshotRequests.Count);
            Assert.Contains("offset 0;", screenshotRequests[0].Body);
            Assert.Contains("offset 500;", screenshotRequests[1].Body);
            Assert.Equal(1, handler.Requests.Count(r => r.Uri.AbsolutePath == "/v4/artworks" && r.Body.Contains(x.ToString())));
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(Prefix);
        }
    }
}
