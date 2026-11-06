using System.Security.Claims;
using System.Text.Json;
using Dapper;
using GamePlanner.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

/// <summary>
/// UI-01 / IMG-02 read path: GET /api/gameimages filtered per local game, user-scoped.
/// Every probe row carries the "gsd-probe-ctl-" name prefix and is purged before and after each fact.
/// Image rows are written through sp_UpsertGameImage; the arrange UPDATEs run under owner decision D-23 (test database only).
/// </summary>
[Collection(TestDatabase.CollectionName)]
public class GameImagesControllerTests
{
    private const string Prefix = "gsd-probe-ctl-";

    private record Arranged(int UserA, int UserB, int A1, int A2, int A3, int B1);

    private static long NewIgdbId() => Random.Shared.NextInt64(1_000_000_000, 2_000_000_000);

    private static string NewName() => $"{Prefix}{Guid.NewGuid():N}";

    private static async Task SetIgdbIdAsync(int gameId, long igdbId)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync("UPDATE Games SET IGDBId = @igdbId WHERE Id = @gameId", new { gameId, igdbId });
    }

    private static async Task AddImageAsync(int gameId, string imageId, string type)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync("sp_UpsertGameImage",
            new
            {
                GameId = gameId,
                IgdbImageId = imageId,
                ImageType = type,
                Url = $"https://images.igdb.com/igdb/image/upload/t_screenshot_big/{imageId}.jpg",
                Width = 1920,
                Height = 1080
            },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    private static async Task<Arranged> ArrangeAsync()
    {
        var a = await TestDatabase.EnsureUserAsync("gsd-probe-ctl-a@test.local");
        var b = await TestDatabase.EnsureUserAsync("gsd-probe-ctl-b@test.local");
        var x = NewIgdbId();
        var y = x + 1;

        var a1 = await TestDatabase.InsertProbeGameAsync(NewName(), a);
        var a2 = await TestDatabase.InsertProbeGameAsync(NewName(), a);
        var a3 = await TestDatabase.InsertProbeGameAsync(NewName(), a);
        var b1 = await TestDatabase.InsertProbeGameAsync(NewName(), b);
        await SetIgdbIdAsync(a1, x);
        await SetIgdbIdAsync(a2, x);
        await SetIgdbIdAsync(a3, y);
        await SetIgdbIdAsync(b1, x);

        foreach (var g in new[] { a1, a2 })
        {
            foreach (var k in new[] { "k1", "k2", "k3" })
                await AddImageAsync(g, $"{k}-{Guid.NewGuid():N}", "screenshot");
            await AddImageAsync(g, $"w1-{Guid.NewGuid():N}", "artwork");
        }
        foreach (var k in new[] { "k1", "k2" })
            await AddImageAsync(b1, $"{k}-{Guid.NewGuid():N}", "screenshot");

        return new Arranged(a, b, a1, a2, a3, b1);
    }

    private static GameImagesController CreateController(int userId) =>
        new(TestDatabase.CreateContext())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };

    private static async Task<List<JsonElement>> GetAsync(int userId, string type = "screenshot", long? igdbId = null, int? gameId = null)
    {
        var result = await CreateController(userId).Get(type, igdbId, gameId);
        var json = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        return JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    [Fact]
    public async Task GameIdFilter_ReturnsOnlyThatGamesScreenshots_InIdOrder()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var d = await ArrangeAsync();

            var rows1 = await GetAsync(d.UserA, gameId: d.A1);
            Assert.Equal(3, rows1.Count);
            Assert.All(rows1, r => Assert.Equal(d.A1, r.GetProperty("GameId").GetInt32()));
            var ids = rows1.Select(r => r.GetProperty("Id").GetInt32()).ToList();
            Assert.Equal(ids.OrderBy(i => i).ToList(), ids);

            var rows2 = await GetAsync(d.UserA, gameId: d.A2);
            Assert.Equal(3, rows2.Count);
            Assert.All(rows2, r => Assert.Equal(d.A2, r.GetProperty("GameId").GetInt32()));
        }
        finally { await TestDatabase.PurgeProbeRowsAsync(Prefix); }
    }

    [Fact]
    public async Task GameIdFilter_ForAnotherUsersGame_ReturnsEmpty()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var d = await ArrangeAsync();
            Assert.Empty(await GetAsync(d.UserA, gameId: d.B1));
        }
        finally { await TestDatabase.PurgeProbeRowsAsync(Prefix); }
    }

    [Fact]
    public async Task GameIdFilter_ForGameWithoutImages_ReturnsEmpty()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var d = await ArrangeAsync();
            Assert.Empty(await GetAsync(d.UserA, gameId: d.A3));
        }
        finally { await TestDatabase.PurgeProbeRowsAsync(Prefix); }
    }

    [Fact]
    public async Task IgdbIdFilter_StillWorks_AndShowsWhySiblingsDuplicate()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var d = await ArrangeAsync();
            await using var conn = new SqlConnection(TestDatabase.ConnectionString);
            var x = await conn.ExecuteScalarAsync<long>("SELECT IGDBId FROM Games WHERE Id = @id", new { id = d.A1 });

            Assert.Equal(6, (await GetAsync(d.UserA, igdbId: x)).Count);
            Assert.Equal(3, (await GetAsync(d.UserA, igdbId: x, gameId: d.A1)).Count);
        }
        finally { await TestDatabase.PurgeProbeRowsAsync(Prefix); }
    }

    [Fact]
    public async Task TypeFilter_KeepsArtworksOutOfScreenshotRequests()
    {
        await TestDatabase.PurgeProbeRowsAsync(Prefix);
        try
        {
            var d = await ArrangeAsync();

            var artworks = await GetAsync(d.UserA, type: "artwork", gameId: d.A1);
            Assert.Single(artworks);

            var shots = await GetAsync(d.UserA, type: "screenshot", gameId: d.A1);
            Assert.DoesNotContain(shots, r => r.GetProperty("ImageType").GetString() == "artwork");
        }
        finally { await TestDatabase.PurgeProbeRowsAsync(Prefix); }
    }
}
