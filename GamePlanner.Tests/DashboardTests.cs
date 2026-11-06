// CONVENTION EXEMPTION (owner decision D-23): arrange helpers use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Managers;
using GamePlanner.API.Managers.WhatToPlay;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class DashboardTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}dash-{Guid.NewGuid():N}-";

    private static DashboardManager CreateManager()
    {
        var db = TestDatabase.CreateContext();
        var config = TestDatabase.CreateConfig();
        return new(db, new NowPlayingManager(db, config), new WhatToPlayManager(db, new UserSettingsManager(db, config)));
    }

    private static async Task ExecAsync(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(sql, args);
    }

    private static Task SetStatusAsync(int user, int game, string status) =>
        ExecAsync("INSERT INTO GameJournalEntries (UserId, GameId, Status) VALUES (@user, @game, @status)", new { user, game, status });

    private static Task LogAsync(int user, int game, decimal hours) =>
        ExecAsync("INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @game, 'PC', '2026-01-01', @hours)",
            new { user, game, hours });

    [Fact]
    public async Task QuickStats_UseTotalHours_SessionsWhenNoSteam()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var steam = await MvpTestData.CreateGameAsync(user, prefix + "steam", steamHours: 4);
            var logged = await MvpTestData.CreateGameAsync(user, prefix + "logged");
            await LogAsync(user, logged, 6);
            await LogAsync(user, steam, 20); // ignored: Steam hours win

            var stats = (await CreateManager().GetDashboardAsync(user)).QuickStats;

            Assert.Equal(10, stats.TotalPlaytimeHours);
            Assert.Equal(prefix + "logged", stats.MostPlayedGame);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task ContinuePlaying_CarriesTotalHours()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            await LogAsync(user, game, 2.5m);
            await ExecAsync("INSERT INTO NowPlayingGames (UserId, GameId, Device) VALUES (@user, @game, 'General')", new { user, game });

            var g = Assert.Single((await CreateManager().GetDashboardAsync(user)).ContinuePlaying);

            Assert.Equal(0, g.PlaytimeHours);
            Assert.Equal(2.5, g.TotalHours);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task BacklogSummary_NoEntryIsBacklog_UnplayedUsesStatsRule_IgnoresDeletedGames()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.CreateGameAsync(user, prefix + "fresh");                    // no entry: Backlog, unplayed
            var backlogPlayed = await MvpTestData.CreateGameAsync(user, prefix + "bp");    // Backlog with hours: played
            await SetStatusAsync(user, backlogPlayed, "Backlog");
            await LogAsync(user, backlogPlayed, 1);
            var paused = await MvpTestData.CreateGameAsync(user, prefix + "paused");      // Paused, 0 h: not unplayed
            await SetStatusAsync(user, paused, "Paused");
            var finished = await MvpTestData.CreateGameAsync(user, prefix + "fin");
            await SetStatusAsync(user, finished, "Finished");
            var deleted = await MvpTestData.CreateGameAsync(user, prefix + "deleted");
            await SetStatusAsync(user, deleted, "Finished");
            await ExecAsync("UPDATE Games SET IsActive = 0 WHERE Id = @deleted", new { deleted });

            var summary = (await CreateManager().GetDashboardAsync(user)).BacklogSummary;

            Assert.Equal(2, summary.TotalBacklog);
            Assert.Equal(1, summary.Completed);
            Assert.Equal(1, summary.Unplayed);
            Assert.Equal(33.3, summary.CompletionRate);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
