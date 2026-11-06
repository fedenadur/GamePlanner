// CONVENTION EXEMPTION (owner decision D-23): arrange/assert helpers use direct SQL against the _Test database.
using System.Security.Claims;
using Dapper;
using GamePlanner.API.Controllers;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class PlaySessionTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}ps-{Guid.NewGuid():N}-";

    // A user with the default devices, so PC and SteamDeck sessions are valid.
    private static async Task<int> CreateUserAsync(string prefix)
    {
        var user = await MvpTestData.CreateUserAsync(prefix);
        await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC, DeviceTypes.SteamDeck);
        return user;
    }

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
            var user = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
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
            var owner = await CreateUserAsync(prefix);
            var intruder = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();

            var future = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game,
                new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = DateTime.UtcNow.Date.AddDays(1) }));
            Assert.Equal("Date cannot be in the future.", future.Message);

            var device = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game, Req(device: "Nope")));
            Assert.Equal("Unknown device.", device.Message);

            var general = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game, Req(device: DeviceTypes.General)));
            Assert.Equal("Unknown device.", general.Message);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_DeviceNotOwned_Rejected_GeneralOnlyWithoutDevices()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();

            var notOwned = await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(user, game, Req(device: DeviceTypes.Switch)));
            Assert.Equal("Unknown device.", notOwned.Message);

            var noDevices = await MvpTestData.CreateUserAsync(prefix);
            var other = await MvpTestData.CreateGameAsync(noDevices, prefix + "h");
            await Assert.ThrowsAsync<ArgumentException>(() => manager.LogAsync(noDevices, other, Req(device: DeviceTypes.PC)));
            Assert.NotNull(await manager.LogAsync(noDevices, other, Req(device: DeviceTypes.General)));
            Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM PlaySessions WHERE UserId IN (@user, @noDevices)", new { user, noDevices }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_KeepsRemovedDevice_ButCannotMoveToAnotherUnownedDevice()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var id = (await manager.LogAsync(user, game, Req(1, DeviceTypes.SteamDeck)))!.Value;
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);

            Assert.True(await manager.UpdateAsync(user, id, Req(2, DeviceTypes.SteamDeck)));
            await Assert.ThrowsAsync<ArgumentException>(() => manager.UpdateAsync(user, id, Req(2, DeviceTypes.Xbox)));
            Assert.True(await manager.UpdateAsync(user, id, Req(3, DeviceTypes.PC)));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Controller_UnownedDevice_Returns400()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var controller = new PlaySessionsController(CreateManager())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test"))
                    }
                }
            };

            Assert.IsType<BadRequestObjectResult>(await controller.Log(game, Req(device: DeviceTypes.Mobile)));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_NeverTouchesSteamPlaytime()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
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
            var owner = await CreateUserAsync(prefix);
            var intruder = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
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
            var user = await CreateUserAsync(prefix);
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

    private static async Task DeactivateAsync(int game)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync("UPDATE Games SET IsActive = 0 WHERE Id = @game", new { game });
    }

    [Fact]
    public async Task Update_SessionOfDeletedGame_ReturnsFalse()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var id = (await manager.LogAsync(user, game, Req(1)))!.Value;
            await DeactivateAsync(game);

            Assert.False(await manager.UpdateAsync(user, id, Req(5)));
            Assert.Equal(1m, await ScalarAsync<decimal>("SELECT Hours FROM PlaySessions WHERE Id=@id", new { id }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Delete_SessionOfDeletedGame_ReturnsFalse()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var id = (await manager.LogAsync(user, game, Req(1)))!.Value;
            await DeactivateAsync(game);

            Assert.False(await manager.DeleteAsync(user, id));
            Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM PlaySessions WHERE Id=@id", new { id }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_Backdated_DoesNotMoveLastPlayedAtBack()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var recent = DateTime.UtcNow.Date.AddDays(-1);
            await manager.LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = recent });
            await manager.LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = recent.AddDays(-10) });

            Assert.Equal(recent, await ScalarAsync<DateTime>("SELECT LastPlayedAt FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Delete_LatestSession_RecomputesLastPlayedAt()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();
            var older = DateTime.UtcNow.Date.AddDays(-5);
            await manager.LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = older });
            var latest = (await manager.LogAsync(user, game, new LogSessionRequest { Device = DeviceTypes.PC, Hours = 1, PlayedOn = older.AddDays(3) }))!.Value;

            Assert.True(await manager.DeleteAsync(user, latest));

            Assert.Equal(older, await ScalarAsync<DateTime>("SELECT LastPlayedAt FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Theory]
    [InlineData(0.1, true)]
    [InlineData(24, true)]
    [InlineData(0.05, false)]
    [InlineData(0.054, false)]
    public async Task Log_HoursBoundaries(double hours, bool accepted)
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var task = CreateManager().LogAsync(user, game, Req((decimal)hours));
            if (accepted) Assert.NotNull(await task);
            else await Assert.ThrowsAsync<ArgumentException>(() => task);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Log_NullDevice_IsUnknownDevice()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateManager().LogAsync(user, game, new LogSessionRequest { Device = null!, Hours = 1 }));
            Assert.Equal("Unknown device.", ex.Message);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
