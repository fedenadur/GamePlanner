using Dapper;
using GamePlanner.API.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class MvpSchemaTests
{
    private const string NamePrefix = "gsd-probe-mvp-";

    [Fact]
    public async Task Schema_HasMvpTables()
    {
        await using var db = TestDatabase.CreateContext();
        await db.UserDevices.AnyAsync();
        await db.PlaySessions.AnyAsync();
        await db.UserPreferences.AnyAsync();
        await db.UserPreferenceGenres.AnyAsync();
        await db.GamePlayableDevices.AnyAsync();
    }

    [Fact]
    public async Task NowPlaying_RejectsSameGameOnTwoDevices()
    {
        var userId = await TestDatabase.EnsureUserAsync("gsd-probe-mvp-schema@test.local");
        var gameId = await TestDatabase.InsertProbeGameAsync($"{NamePrefix}{Guid.NewGuid():N}", userId);
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        try
        {
            await conn.ExecuteAsync("INSERT INTO NowPlayingGames (UserId, GameId, Device) VALUES (@userId, @gameId, 'PC')", new { userId, gameId });
            var ex = await Assert.ThrowsAsync<SqlException>(() =>
                conn.ExecuteAsync("INSERT INTO NowPlayingGames (UserId, GameId, Device) VALUES (@userId, @gameId, 'Switch')", new { userId, gameId }));
            Assert.Equal(2627, ex.Number);
        }
        finally
        {
            await conn.ExecuteAsync("DELETE FROM NowPlayingGames WHERE UserId = @userId AND GameId = @gameId", new { userId, gameId });
            await TestDatabase.PurgeProbeRowsAsync(NamePrefix);
        }
    }

    [Fact]
    public async Task PlaySessions_RejectsZeroHours()
    {
        var userId = await TestDatabase.EnsureUserAsync("gsd-probe-mvp-schema@test.local");
        var gameId = await TestDatabase.InsertProbeGameAsync($"{NamePrefix}{Guid.NewGuid():N}", userId);
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<SqlException>(() =>
                conn.ExecuteAsync("INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@userId, @gameId, 'PC', '2026-01-01', 0)", new { userId, gameId }));
            Assert.Equal(547, ex.Number);
        }
        finally
        {
            await TestDatabase.PurgeProbeRowsAsync(NamePrefix);
        }
    }
}
