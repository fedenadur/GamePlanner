// CONVENTION EXEMPTION (owner decision D-23): the arrange/assert helpers below use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class NowPlayingTransitionTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}np-{Guid.NewGuid():N}-";

    private static NowPlayingManager CreateManager() =>
        new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static GameJournalManager CreateJournalManager() =>
        new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static async Task<T> QueryAsync<T>(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        return await conn.QuerySingleAsync<T>(sql, args);
    }

    private static Task<JournalRow> GetJournalAsync(int userId, int gameId) =>
        QueryAsync<JournalRow>(
            "SELECT Status, StartedAt, FinishedAt, UserRating, ReplayCount FROM GameJournalEntries WHERE UserId = @userId AND GameId = @gameId",
            new { userId, gameId });

    private static Task<int> CountNowPlayingAsync(int userId) =>
        QueryAsync<int>("SELECT COUNT(*) FROM NowPlayingGames WHERE UserId = @userId", new { userId });

    private record JournalRow(string Status, DateTime? StartedAt, DateTime? FinishedAt, byte? UserRating, int ReplayCount);

    private static StartNowPlayingRequest Start(int gameId, string? outcome = null, byte? rating = null) =>
        new() { GameId = gameId, OutgoingOutcome = outcome, OutgoingRating = rating };

    [Fact]
    public async Task Start_SetsJournalPlaying_AndStartedAt()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");

            var result = await CreateManager().StartAsync(user, DeviceTypes.PC, Start(game));

            Assert.Equal(NowPlayingResult.Ok, result);
            var journal = await GetJournalAsync(user, game);
            Assert.Equal(JournalStatuses.Playing, journal.Status);
            Assert.NotNull(journal.StartedAt);
            Assert.Equal(1, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_FinishedGame_BecomesReplaying_AndIncrementsReplayCount()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
                await conn.ExecuteAsync(
                    "INSERT INTO GameJournalEntries (UserId, GameId, Status, ReplayCount) VALUES (@user, @game, 'Finished', 2)",
                    new { user, game });

            var result = await CreateManager().StartAsync(user, DeviceTypes.PC, Start(game));

            Assert.Equal(NowPlayingResult.Ok, result);
            var journal = await GetJournalAsync(user, game);
            Assert.Equal(JournalStatuses.Replaying, journal.Status);
            Assert.Equal(3, journal.ReplayCount);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_OccupiedSlotWithoutOutcome_ReturnsSlotOccupied_AndChangesNothing()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var first = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var second = await MvpTestData.CreateGameAsync(user, prefix + "b");
            var manager = CreateManager();
            Assert.Equal(NowPlayingResult.Ok, await manager.StartAsync(user, DeviceTypes.PC, Start(first)));

            var result = await manager.StartAsync(user, DeviceTypes.PC, Start(second));

            Assert.Equal(NowPlayingResult.SlotOccupied, result);
            Assert.Equal(first, await QueryAsync<int>("SELECT GameId FROM NowPlayingGames WHERE UserId = @user", new { user }));
            Assert.Equal(JournalStatuses.Playing, (await GetJournalAsync(user, first)).Status);
            Assert.Equal(0, await QueryAsync<int>("SELECT COUNT(*) FROM GameJournalEntries WHERE UserId = @user AND GameId = @second", new { user, second }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_OccupiedSlotWithFinished_FinishesOutgoing_SetsFinishedAtAndRating()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var first = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var second = await MvpTestData.CreateGameAsync(user, prefix + "b");
            var manager = CreateManager();
            await manager.StartAsync(user, DeviceTypes.PC, Start(first));

            var result = await manager.StartAsync(user, DeviceTypes.PC, Start(second, JournalStatuses.Finished, 8));

            Assert.Equal(NowPlayingResult.Ok, result);
            var outgoing = await GetJournalAsync(user, first);
            Assert.Equal(JournalStatuses.Finished, outgoing.Status);
            Assert.NotNull(outgoing.FinishedAt);
            Assert.Equal((byte)8, outgoing.UserRating);
            Assert.Equal(JournalStatuses.Playing, (await GetJournalAsync(user, second)).Status);
            Assert.Equal(second, await QueryAsync<int>("SELECT GameId FROM NowPlayingGames WHERE UserId = @user", new { user }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_GameOnOtherDevice_ReturnsGameOnOtherDevice()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC, DeviceTypes.SteamDeck);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var manager = CreateManager();
            await manager.StartAsync(user, DeviceTypes.PC, Start(game));

            var result = await manager.StartAsync(user, DeviceTypes.SteamDeck, Start(game));

            Assert.Equal(NowPlayingResult.GameOnOtherDevice, result);
            Assert.Equal(1, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_OtherUsersGame_ReturnsGameNotFound()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var other = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(other, prefix + "a");

            var result = await CreateManager().StartAsync(user, DeviceTypes.PC, Start(game));

            Assert.Equal(NowPlayingResult.GameNotFound, result);
            Assert.Equal(0, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_DeviceNotInUserList_ReturnsInvalidDevice()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");

            var result = await CreateManager().StartAsync(user, DeviceTypes.Switch, Start(game));

            Assert.Equal(NowPlayingResult.InvalidDevice, result);
            Assert.Equal(0, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Start_ConcurrentOnSameDevice_OneOkOneConflict()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var first = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var second = await MvpTestData.CreateGameAsync(user, prefix + "b");

            var results = await Task.WhenAll(
                CreateManager().StartAsync(user, DeviceTypes.PC, Start(first)),
                CreateManager().StartAsync(user, DeviceTypes.PC, Start(second)));

            Assert.Equal(1, results.Count(r => r == NowPlayingResult.Ok));
            Assert.Equal(1, results.Count(r => r == NowPlayingResult.SlotOccupied));
            Assert.Equal(1, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Remove_Dropped_SetsStatus_AndFreesSlot()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var manager = CreateManager();
            await manager.StartAsync(user, DeviceTypes.PC, Start(game));

            var result = await manager.RemoveAsync(user, DeviceTypes.PC, new RemoveNowPlayingRequest { Outcome = JournalStatuses.Dropped });

            Assert.Equal(NowPlayingResult.Ok, result);
            Assert.Equal(JournalStatuses.Dropped, (await GetJournalAsync(user, game)).Status);
            Assert.Equal(0, await CountNowPlayingAsync(user));
            Assert.Equal(NowPlayingResult.SlotEmpty,
                await manager.RemoveAsync(user, DeviceTypes.PC, new RemoveNowPlayingRequest { Outcome = JournalStatuses.Dropped }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Remove_None_KeepsJournalStatus()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            var manager = CreateManager();
            await manager.StartAsync(user, DeviceTypes.PC, Start(game));

            var result = await manager.RemoveAsync(user, DeviceTypes.PC, new RemoveNowPlayingRequest { Outcome = "None" });

            Assert.Equal(NowPlayingResult.Ok, result);
            Assert.Equal(JournalStatuses.Playing, (await GetJournalAsync(user, game)).Status);
            Assert.Equal(0, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task JournalUpsert_ToFinished_RemovesFromNowPlaying()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            await CreateManager().StartAsync(user, DeviceTypes.PC, Start(game));

            var ok = await CreateJournalManager().UpsertAsync(user, game, new UpsertJournalRequest { Status = JournalStatuses.Finished });

            Assert.True(ok);
            Assert.Equal(0, await CountNowPlayingAsync(user));
            Assert.Equal(JournalStatuses.Finished, (await GetJournalAsync(user, game)).Status);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task JournalUpsert_OtherUsersGame_ReturnsFalse_AndWritesNothing()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var other = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(other, prefix + "a");

            var ok = await CreateJournalManager().UpsertAsync(user, game, new UpsertJournalRequest { Status = JournalStatuses.Playing });

            Assert.False(ok);
            Assert.Equal(0, await QueryAsync<int>("SELECT COUNT(*) FROM GameJournalEntries WHERE GameId = @game", new { game }));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task DeleteGame_ClearsNowPlaying()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "a");
            await CreateManager().StartAsync(user, DeviceTypes.PC, Start(game));

            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
            {
                var rows = await conn.QuerySingleAsync<int>("sp_DeleteGame", new { Id = game }, commandType: System.Data.CommandType.StoredProcedure);
                Assert.Equal(1, rows);
            }

            Assert.Equal(0, await CountNowPlayingAsync(user));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task GetSlots_NoDevices_ReturnsSingleGeneralSlot()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);

            var slots = await CreateManager().GetSlotsAsync(user);

            var slot = Assert.Single(slots);
            Assert.Equal(DeviceTypes.General, slot.Device);
            Assert.Null(slot.Game);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
