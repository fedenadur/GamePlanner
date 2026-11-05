using Dapper;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class StatsTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}st-{Guid.NewGuid():N}-";

    private static StatsDashboardManager CreateManager() => new(TestDatabase.CreateContext());

    private static async Task ExecAsync(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(sql, args);
    }

    [Fact]
    public async Task Stats_ExcludesDeletedGames()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.CreateGameAsync(user, prefix + "a", steamHours: 1);
            await MvpTestData.CreateGameAsync(user, prefix + "b", steamHours: 2);
            await MvpTestData.CreateGameAsync(user, prefix + "c", steamHours: 3);
            var deleted = await MvpTestData.CreateGameAsync(user, prefix + "deleted", steamHours: 500);
            await ExecAsync("UPDATE Games SET IsActive = 0 WHERE Id = @deleted", new { deleted });

            var stats = await CreateManager().GetDashboardAsync(user);

            Assert.Equal(3, stats.Library.TotalOwned);
            Assert.Equal(prefix + "c", stats.Time.MostPlayedGame);
            Assert.Equal(6, stats.Time.TotalPlaytimeHours);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Stats_UsesLoggedHours_WhenNoSteamHours()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var logged = await MvpTestData.CreateGameAsync(user, prefix + "logged");
            await MvpTestData.CreateGameAsync(user, prefix + "steam", steamHours: 4);
            await ExecAsync(
                "INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @logged, 'PC', GETDATE(), 10)",
                new { user, logged });

            var stats = await CreateManager().GetDashboardAsync(user);

            Assert.Equal(14, stats.Time.TotalPlaytimeHours);
            Assert.Equal(prefix + "logged", stats.Time.MostPlayedGame);
            Assert.Equal(10, stats.Time.MostPlayedGameHours);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Stats_NeverPlayed_IsZeroHoursAndBacklog()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.CreateGameAsync(user, prefix + "untouched");
            var logged = await MvpTestData.CreateGameAsync(user, prefix + "logged");
            var playing = await MvpTestData.CreateGameAsync(user, prefix + "playing");
            await ExecAsync(
                "INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @logged, 'PC', GETDATE(), 1)",
                new { user, logged });
            await ExecAsync(
                "INSERT INTO GameJournalEntries (UserId, GameId, Status) VALUES (@user, @playing, 'Playing')",
                new { user, playing });

            var stats = await CreateManager().GetDashboardAsync(user);

            Assert.Equal(1, stats.Library.NeverPlayed);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
