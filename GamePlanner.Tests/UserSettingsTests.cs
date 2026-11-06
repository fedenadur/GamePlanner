// CONVENTION EXEMPTION (owner decision D-23): arrange/assert helpers use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class UserSettingsTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}us-{Guid.NewGuid():N}-";

    private static UserSettingsManager CreateManager() =>
        new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static async Task<List<string>> DeviceRowsAsync(int userId)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        return (await conn.QueryAsync<string>(
            "SELECT Device FROM UserDevices WHERE UserId = @userId ORDER BY SortOrder", new { userId })).ToList();
    }

    [Fact]
    public async Task SaveDevices_KeepsOrder()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var manager = CreateManager();

            var result = await manager.SaveDevicesAsync(user, [DeviceTypes.Switch, DeviceTypes.PC, DeviceTypes.Mobile]);

            Assert.Equal(SaveDevicesResult.Ok, result);
            Assert.Equal([DeviceTypes.Switch, DeviceTypes.PC, DeviceTypes.Mobile], await manager.GetDevicesAsync(user));

            Assert.Equal(SaveDevicesResult.Ok, await manager.SaveDevicesAsync(user, [DeviceTypes.Mobile, DeviceTypes.Switch]));
            Assert.Equal([DeviceTypes.Mobile, DeviceTypes.Switch], await manager.GetDevicesAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task SaveDevices_RejectsGeneralAndDuplicates()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var manager = CreateManager();

            Assert.Equal(SaveDevicesResult.Invalid, await manager.SaveDevicesAsync(user, [DeviceTypes.General]));
            Assert.Equal(SaveDevicesResult.Invalid, await manager.SaveDevicesAsync(user, [DeviceTypes.PC, DeviceTypes.PC]));
            Assert.Equal(SaveDevicesResult.Invalid, await manager.SaveDevicesAsync(user, ["Nope"]));
            Assert.Equal([DeviceTypes.PC], await DeviceRowsAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task SaveDevices_RemovingDeviceWithGame_ReturnsDeviceInUse()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC, DeviceTypes.Switch);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var np = new NowPlayingManager(TestDatabase.CreateContext(), TestDatabase.CreateConfig());
            Assert.Equal(NowPlayingResult.Ok,
                await np.StartAsync(user, DeviceTypes.Switch, new StartNowPlayingRequest { GameId = game }));
            var manager = CreateManager();

            var result = await manager.SaveDevicesAsync(user, [DeviceTypes.PC]);

            Assert.Equal(SaveDevicesResult.DeviceInUse, result);
            Assert.Equal([DeviceTypes.PC, DeviceTypes.Switch], await DeviceRowsAsync(user));

            // keeping the used device is fine, reordering included
            Assert.Equal(SaveDevicesResult.Ok, await manager.SaveDevicesAsync(user, [DeviceTypes.Switch, DeviceTypes.Xbox]));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task SaveDevices_FirstDevice_MovesGeneralGameToIt()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var np = new NowPlayingManager(TestDatabase.CreateContext(), TestDatabase.CreateConfig());
            Assert.Equal(NowPlayingResult.Ok,
                await np.StartAsync(user, DeviceTypes.General, new StartNowPlayingRequest { GameId = game }));

            var result = await CreateManager().SaveDevicesAsync(user, [DeviceTypes.SteamDeck, DeviceTypes.PC]);

            Assert.Equal(SaveDevicesResult.Ok, result);
            await using var conn = new SqlConnection(TestDatabase.ConnectionString);
            Assert.Equal(DeviceTypes.SteamDeck, await conn.ExecuteScalarAsync<string>(
                "SELECT Device FROM NowPlayingGames WHERE UserId = @user AND GameId = @game", new { user, game }));
            Assert.Equal(JournalStatuses.Playing, await conn.ExecuteScalarAsync<string>(
                "SELECT Status FROM GameJournalEntries WHERE UserId = @user AND GameId = @game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Lists_WithBlankElements_AreRejected()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var manager = CreateManager();

            Assert.Equal(SaveDevicesResult.Invalid, await manager.SaveDevicesAsync(user, [null!]));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto { FavoriteGenres = [" "] }));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto { AvoidGenres = [null!] }));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto
            { FavoriteGenres = ["rpg"], AvoidGenres = ["RPG"] }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Preferences_DefaultWhenMissing()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);

            var p = await CreateManager().GetPreferencesAsync(user);

            Assert.Empty(p.FavoriteGenres);
            Assert.Empty(p.AvoidGenres);
            Assert.Null(p.MaxGameHours);
            Assert.Equal("Neutral", p.SinglePlayer);
            Assert.Equal("Neutral", p.Narrative);
            Assert.Equal("Neutral", p.Combat);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Preferences_RoundTrip()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var gA = prefix + "RPG";
            var gB = prefix + "Puzzle";
            var gC = prefix + "Shooter";
            await MvpTestData.CreateGameAsync(user, prefix + "g", genres: [gA, gB, gC]);
            var manager = CreateManager();

            var ok = await manager.SavePreferencesAsync(user, new UserPreferencesDto
            {
                FavoriteGenres = [gA, gB], AvoidGenres = [gC, prefix + "DoesNotExist"],
                MaxGameHours = 40, SinglePlayer = "Prefer", Narrative = "Avoid", Combat = "Neutral"
            });

            Assert.True(ok);
            var p = await manager.GetPreferencesAsync(user);
            Assert.Equal([gB, gA], p.FavoriteGenres);
            Assert.Equal([gC], p.AvoidGenres);
            Assert.Equal(40, p.MaxGameHours);
            Assert.Equal("Prefer", p.SinglePlayer);
            Assert.Equal("Avoid", p.Narrative);

            // second save replaces everything, null hours allowed
            Assert.True(await manager.SavePreferencesAsync(user, new UserPreferencesDto { FavoriteGenres = [gC] }));
            p = await manager.GetPreferencesAsync(user);
            Assert.Equal([gC], p.FavoriteGenres);
            Assert.Empty(p.AvoidGenres);
            Assert.Null(p.MaxGameHours);
            Assert.Equal("Neutral", p.SinglePlayer);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Preferences_GenreInBothLists_Rejected()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var manager = CreateManager();

            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto
            { FavoriteGenres = ["RPG"], AvoidGenres = ["RPG"] }));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto { MaxGameHours = 0 }));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto { MaxGameHours = 501 }));
            Assert.False(await manager.SavePreferencesAsync(user, new UserPreferencesDto { Combat = "Maybe" }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Preferences_GenreWithComma_RoundTrips()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            const string commaGenre = "Hack and slash/Beat 'em up";
            var custom = prefix + "Strategy, turn-based";
            await MvpTestData.CreateGameAsync(user, prefix + "g", genres: [commaGenre, custom]);
            var manager = CreateManager();

            Assert.True(await manager.SavePreferencesAsync(user, new UserPreferencesDto
            { FavoriteGenres = [commaGenre], AvoidGenres = [custom] }));

            var p = await manager.GetPreferencesAsync(user);
            Assert.Equal([commaGenre], p.FavoriteGenres);
            Assert.Equal([custom], p.AvoidGenres);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task JournalDelete_RemovesFromNowPlaying()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var np = new NowPlayingManager(TestDatabase.CreateContext(), TestDatabase.CreateConfig());
            Assert.Equal(NowPlayingResult.Ok,
                await np.StartAsync(user, DeviceTypes.PC, new StartNowPlayingRequest { GameId = game }));

            await new GameJournalManager(TestDatabase.CreateContext(), TestDatabase.CreateConfig()).DeleteAsync(user, game);

            await using var conn = new SqlConnection(TestDatabase.ConnectionString);
            Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM NowPlayingGames WHERE UserId = @user", new { user }));
            Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GameJournalEntries WHERE UserId = @user AND GameId = @game", new { user, game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
