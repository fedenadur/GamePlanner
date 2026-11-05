// CONVENTION EXEMPTION (owner decision D-23): arrange/assert helpers use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class PlaySessionTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}ps-{Guid.NewGuid():N}-";

    private static PlaySessionManager CreateManager() =>
        new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static LogSessionRequest Req(decimal hours = 1.5m, string device = DeviceTypes.PC) =>
        new() { Device = device, Hours = hours };

    private static async Task<T> ScalarAsync<T>(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        return await conn.ExecuteScalarAsync<T>(sql, args) ?? default!;
    }

    [Fact]
    public async Task Log_UpdatesLastPlayedAt_WithoutChangingStatus()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
                await conn.ExecuteAsync(
                    "INSERT INTO GameJournalEntries (UserId, GameId, Status) VALUES (@user, @game, 'Playing')",
                    new { user, game });
            var day = DateTime.UtcNow.Date.AddDays(-2);

            var id = await CreateManager().LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = 2, PlayedOn = day });

            Assert.NotNull(id);
            Assert.Equal("Playing", await ScalarAsync<string>("SELECT Status FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game }));
            Assert.Equal(day, await ScalarAsync<DateTime>("SELECT LastPlayedAt FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_WithoutJournalEntry_CreatesBacklogEntry()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");

            await CreateManager().LogAsync(user, game, Req());

            Assert.Equal("Backlog", await ScalarAsync<string>("SELECT Status FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_OtherUsersGame_ReturnsNull()
    {
        var prefix = NewPrefix();
        try
        {
            var owner = await MvpTestData.CreateUserAsync(prefix);
            var intruder = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(owner, prefix + "g");

            Assert.Null(await CreateManager().LogAsync(intruder, game, Req()));
            Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM PlaySessions WHERE GameId=@game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24.5)]
    public async Task Log_HoursOutOfRange_Rejected(double hours)
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => CreateManager().LogAsync(user, game, Req((decimal)hours)));
            Assert.Equal("Hours must be between 0.1 and 24.", ex.Message);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_FutureDateOrUnknownDevice_Rejected()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();

            var future = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game,
                new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = DateTime.UtcNow.Date.AddDays(1) }));
            Assert.Equal("Date cannot be in the future.", future.Message);

            var device = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game, Req(device: "Nope")));
            Assert.Equal("Unknown device.", device.Message);

            Assert.NotNull(await manager.LogAsync(user, game, Req(device: DeviceTypes.General)));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_NeverTouchesSteamPlaytime()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g", steamHours: 42);

            await CreateManager().LogAsync(user, game, Req(3));

            Assert.Equal(42d, await ScalarAsync<double>("SELECT PlaytimeHours FROM Games WHERE Id=@game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_And_Delete_Work_ForOwner()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var id = (await manager.LogAsync(user, game, Req(1)))!.Value;

            Assert.True(await manager.UpdateAsync(user, id, new LogSessionRequest { Device = DeviceTypes.SteamDeck, Hours = 2.5m, Note = "edited" }));
            var s = Assert.Single(await manager.GetForGameAsync(user, game));
            Assert.Equal(2.5m, s.Hours);
            Assert.Equal(DeviceTypes.SteamDeck, s.Device);
            Assert.Equal("edited", s.Note);

            Assert.True(await manager.DeleteAsync(user, id));
            Assert.Empty(await manager.GetForGameAsync(user, game));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Delete_OtherUsersSession_ReturnsFalse()
    {
        var prefix = NewPrefix();
        try
        {
            var owner = await MvpTestData.CreateUserAsync(prefix);
            var intruder = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(owner, prefix + "g");
            var manager = CreateManager();
            var id = (await manager.LogAsync(owner, game, Req()))!.Value;

            Assert.False(await manager.DeleteAsync(intruder, id));
            Assert.False(await manager.UpdateAsync(intruder, id, Req()));
            Assert.Single(await manager.GetForGameAsync(owner, game));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Sessions_OfDeletedGame_NotReturned()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            await manager.LogAsync(user, game, Req());
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
                await conn.ExecuteAsync("UPDATE Games SET IsActive = 0 WHERE Id = @game", new { game });

            Assert.Empty(await manager.GetForGameAsync(user, game));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task NowPlayingSlots_IncludeLastThreeSessions_MostRecentFirst()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            for (var i = 4; i >= 1; i--)
                await manager.LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = i, PlayedOn = DateTime.UtcNow.Date.AddDays(-i) });
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
                await conn.ExecuteAsync("INSERT INTO NowPlayingGames (UserId, GameId, Device) VALUES (@user, @game, 'PC')", new { user, game });

            var slots = await new NowPlayingManager(TestDatabase.CreateContext(), TestDatabase.CreateConfig()).GetSlotsAsync(user);

            Assert.Equal([1m, 2m, 3m], slots.Single().RecentSessions.Select(s => s.Hours));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
