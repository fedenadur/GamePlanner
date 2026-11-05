// CONVENTION EXEMPTION (owner decision D-23): arrange/assert helpers use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class GameLibraryTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}lib-{Guid.NewGuid():N}-";

    private static GameManager CreateManager() => new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static async Task ExecAsync(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(sql, args);
    }

    private static async Task<T> ScalarAsync<T>(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        return await conn.ExecuteScalarAsync<T>(sql, args) ?? default!;
    }

    private static UpdateGameRequest UpdateReq(string name, string store = "Other", List<string>? devices = null,
        bool favorite = false, string? cover = null) =>
        new() { Name = name, EstimatedMinutes = 30, Store = store, PlayableDevices = devices ?? [], IsFavorite = favorite, CoverImageUrl = cover };

    [Fact]
    public async Task GetAll_ReturnsStatusFromJournal_DefaultBacklog()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var played = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var untouched = await MvpTestData.CreateGameAsync(user, prefix + "b");
            await ExecAsync("INSERT INTO GameJournalEntries (UserId, GameId, Status) VALUES (@user, @played, 'Finished')", new { user, played });

            var all = await CreateManager().GetAllAsync(user);

            Assert.Equal("Finished", all.Single(g => g.Id == played).Status);
            Assert.Equal("Backlog", all.Single(g => g.Id == untouched).Status);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task GetAll_TotalHours_UsesSessionsWhenNoSteam()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var manual = await MvpTestData.CreateGameAsync(user, prefix + "m");
            var steam = await MvpTestData.CreateGameAsync(user, prefix + "s", store: "Steam", steamHours: 10);
            foreach (var game in new[] { manual, steam })
            {
                await ExecAsync("INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @game, 'PC', '2026-01-01', 1.5)", new { user, game });
                await ExecAsync("INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @game, 'PC', '2026-01-02', 2)", new { user, game });
            }

            var all = await CreateManager().GetAllAsync(user);

            var m = all.Single(g => g.Id == manual);
            Assert.Equal(3.5, m.LoggedHours);
            Assert.Equal(2, m.SessionCount);
            Assert.Equal(3.5, m.TotalHours);
            var s = all.Single(g => g.Id == steam);
            Assert.Equal(3.5, s.LoggedHours);
            Assert.Equal(10, s.TotalHours);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task GetAll_ReturnsReleaseYear_NullWhenNoDate()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var dated = await MvpTestData.CreateGameAsync(user, prefix + "d");
            var undated = await MvpTestData.CreateGameAsync(user, prefix + "u");
            await ExecAsync("UPDATE Games SET FirstReleaseDate = '2019-06-15' WHERE Id = @dated", new { dated });

            var all = await CreateManager().GetAllAsync(user);

            Assert.Equal(2019, all.Single(g => g.Id == dated).ReleaseYear);
            Assert.Null(all.Single(g => g.Id == undated).ReleaseYear);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_SetsStoreDevicesFavoriteCover()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");

            var ok = await CreateManager().UpdateAsync(game,
                UpdateReq(prefix + "g2", "NintendoEShop", [DeviceTypes.Switch, DeviceTypes.Mobile], true, "https://img/x.jpg"), user);

            Assert.True(ok);
            var dto = (await CreateManager().GetAllAsync(user)).Single(g => g.Id == game);
            Assert.Equal("NintendoEShop", dto.Store);
            Assert.True(dto.IsFavorite);
            Assert.Equal("https://img/x.jpg", dto.CoverImageUrl);
            Assert.Equal([DeviceTypes.Mobile, DeviceTypes.Switch], dto.PlayableDevices.Order().ToList());
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_EmptyDevices_UsesStoreDefaults()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            await ExecAsync("INSERT INTO GamePlayableDevices (GameId, Device) VALUES (@game, 'Xbox')", new { game });

            await CreateManager().UpdateAsync(game, UpdateReq(prefix + "g", "Steam", []), user);

            var dto = (await CreateManager().GetAllAsync(user)).Single(g => g.Id == game);
            Assert.Equal([DeviceTypes.PC, DeviceTypes.SteamDeck], dto.PlayableDevices);
            Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM GamePlayableDevices WHERE GameId=@game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_NullDevices_TreatedAsEmpty()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var request = UpdateReq(prefix + "g", "Steam");
            request.PlayableDevices = null!;

            Assert.True(await CreateManager().UpdateAsync(game, request, user));

            var dto = (await CreateManager().GetAllAsync(user)).Single(g => g.Id == game);
            Assert.Equal([DeviceTypes.PC, DeviceTypes.SteamDeck], dto.PlayableDevices);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Update_InvalidStore_Rejected()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var manager = CreateManager();

            await Assert.ThrowsAsync<ArgumentException>(() => manager.UpdateAsync(game, UpdateReq(prefix + "x", "Nope"), user));
            await Assert.ThrowsAsync<ArgumentException>(() => manager.UpdateAsync(game, UpdateReq(prefix + "x", "Steam", ["Toaster"]), user));
            Assert.Equal(prefix + "g", await ScalarAsync<string>("SELECT Name FROM Games WHERE Id=@game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Favorite_OtherUsersGame_NotFound()
    {
        var prefix = NewPrefix();
        try
        {
            var owner = await MvpTestData.CreateUserAsync(prefix);
            var intruder = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(owner, prefix + "g");
            var manager = CreateManager();

            Assert.False(await manager.SetFavoriteAsync(game, true, intruder));
            Assert.Equal(0, await ScalarAsync<int>("SELECT CAST(IsFavorite AS INT) FROM Games WHERE Id=@game", new { game }));
            Assert.True(await manager.SetFavoriteAsync(game, true, owner));
            Assert.Equal(1, await ScalarAsync<int>("SELECT CAST(IsFavorite AS INT) FROM Games WHERE Id=@game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
