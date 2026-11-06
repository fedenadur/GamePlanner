// CONVENTION EXEMPTION (owner decision D-23): arrange helpers use direct SQL against the _Test database.
using Dapper;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using GamePlanner.API.Managers.WhatToPlay;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

[Collection(TestDatabase.CollectionName)]
public class WhatToPlayManagerTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}wtp-{Guid.NewGuid():N}-";

    private static WhatToPlayManager CreateManager()
    {
        var db = TestDatabase.CreateContext();
        return new(db, new UserSettingsManager(db, TestDatabase.CreateConfig()));
    }

    private static async Task ExecAsync(string sql, object args)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.ExecuteAsync(sql, args);
    }

    private static Task SetStatusAsync(int user, int game, string status, int? rating = null) =>
        ExecAsync("INSERT INTO GameJournalEntries (UserId, GameId, Status, UserRating) VALUES (@user, @game, @status, @rating)",
            new { user, game, status, rating });

    [Fact]
    public async Task Recommend_ExcludesFinishedAndNowPlaying_FromRealData()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var finished = await MvpTestData.CreateGameAsync(user, prefix + "finished");
            var playing = await MvpTestData.CreateGameAsync(user, prefix + "playing");
            var ok = await MvpTestData.CreateGameAsync(user, prefix + "ok");
            await SetStatusAsync(user, finished, JournalStatuses.Finished);
            await ExecAsync("INSERT INTO NowPlayingGames (UserId, GameId, Device) VALUES (@user, @playing, 'PC')", new { user, playing });

            var response = await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 10, CancellationToken.None);

            Assert.Equal([ok], response.Results.Select(r => r.GameId).ToList());
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_UserWithoutDevices_UsesGeneral()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.CreateGameAsync(user, prefix + "a");

            var response = await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 5, CancellationToken.None);

            Assert.Equal(DeviceTypes.General, response.Device);
            Assert.Single(response.Results);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_DefaultDevice_IsFirstOfUser_AndFiltersByStore()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.Switch, DeviceTypes.PC);
            await MvpTestData.CreateGameAsync(user, prefix + "steam", store: Stores.Steam);
            var eshop = await MvpTestData.CreateGameAsync(user, prefix + "eshop", store: Stores.NintendoEShop);

            var response = await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 5, CancellationToken.None);

            Assert.Equal(DeviceTypes.Switch, response.Device);
            Assert.Equal([eshop], response.Results.Select(r => r.GameId).ToList());
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_DeviceNotOwned_Throws()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            await MvpTestData.SetDevicesAsync(user, DeviceTypes.PC);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateManager().RecommendAsync(user, new WhatToPlayRequest { Device = DeviceTypes.Xbox }, 5, CancellationToken.None));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_DeletedGame_NeverReturned()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var deleted = await MvpTestData.CreateGameAsync(user, prefix + "deleted");
            var ok = await MvpTestData.CreateGameAsync(user, prefix + "ok");
            await ExecAsync("UPDATE Games SET IsActive = 0 WHERE Id = @deleted", new { deleted });

            var response = await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 10, CancellationToken.None);

            Assert.Equal([ok], response.Results.Select(r => r.GameId).ToList());
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_NoSteamUser_UsesLoggedHoursForResume()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var started = await MvpTestData.CreateGameAsync(user, prefix + "started");
            await ExecAsync("INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours) VALUES (@user, @started, 'PC', '2026-01-01', 1.5), (@user, @started, 'PC', '2026-01-02', 2)",
                new { user, started });

            var response = await CreateManager().RecommendAsync(user, new WhatToPlayRequest { Strategy = "Resume" }, 5, CancellationToken.None);

            var result = Assert.Single(response.Results);
            Assert.Equal(3.5, result.TotalHours);
            Assert.Contains(result.Reasons, r => r.Text.StartsWith("Already started"));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Recommend_LengthFromHltbBySteamAppIdThenName_ElseEstimatedMinutes()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var bySteam = await MvpTestData.CreateGameAsync(user, prefix + "steamgame");
            var byName = await MvpTestData.CreateGameAsync(user, prefix + "namegame");
            var byEstimate = await MvpTestData.CreateGameAsync(user, prefix + "estimate", estimatedMinutes: 90);
            var placeholder = await MvpTestData.CreateGameAsync(user, prefix + "placeholder", estimatedMinutes: 60);
            var appId = Random.Shared.Next(900_000_000, 999_999_999);
            await ExecAsync("UPDATE Games SET SteamAppId = @appId WHERE Id = @bySteam", new { appId, bySteam });
            await ExecAsync("UPDATE Games SET SteamAppId = @other WHERE Id = @placeholder", new { other = appId - 1, placeholder });
            await ExecAsync("INSERT INTO GamesHLTB (Name, SteamAppId, MainStoryHours) VALUES (@n, @appId, 12)", new { n = prefix + "other", appId });
            await ExecAsync("INSERT INTO GamesHLTB (Name, MainStoryHours) VALUES (@n, 7)", new { n = prefix + "NAMEGAME" });

            var results = (await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 10, CancellationToken.None)).Results;

            Assert.Equal(12, results.Single(r => r.GameId == bySteam).LengthHours);
            Assert.Equal(7, results.Single(r => r.GameId == byName).LengthHours);
            Assert.Equal(1.5, results.Single(r => r.GameId == byEstimate).LengthHours);
            Assert.Null(results.Single(r => r.GameId == placeholder).LengthHours);
        }
        finally
        {
            await ExecAsync("DELETE FROM GamesHLTB WHERE Name LIKE @p", new { p = prefix + "%" });
            await MvpTestData.CleanupAsync(prefix);
        }
    }

    [Fact]
    public async Task Profile_LikedFromRatingAndFavorite_DislikedFromDropped()
    {
        var prefix = NewPrefix();
        var rpg = prefix + "RPG";
        var puzzle = prefix + "Puzzle";
        var horror = prefix + "Horror";
        var racing = prefix + "Racing";
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var rated = await MvpTestData.CreateGameAsync(user, prefix + "rated", genres: [rpg]);
            var fav = await MvpTestData.CreateGameAsync(user, prefix + "fav", genres: [puzzle]);
            var dropped = await MvpTestData.CreateGameAsync(user, prefix + "dropped", genres: [horror]);
            var lowRated = await MvpTestData.CreateGameAsync(user, prefix + "low", genres: [racing]);
            await SetStatusAsync(user, rated, JournalStatuses.Finished, 8);
            await ExecAsync("UPDATE Games SET IsFavorite = 1 WHERE Id = @fav", new { fav });
            await SetStatusAsync(user, dropped, JournalStatuses.Dropped);
            await SetStatusAsync(user, lowRated, JournalStatuses.Paused, 4);

            var cRpg = await MvpTestData.CreateGameAsync(user, prefix + "c-rpg", genres: [rpg]);
            var cPuzzle = await MvpTestData.CreateGameAsync(user, prefix + "c-puzzle", genres: [puzzle]);
            var cHorror = await MvpTestData.CreateGameAsync(user, prefix + "c-horror", genres: [horror]);
            var cRacing = await MvpTestData.CreateGameAsync(user, prefix + "c-racing", genres: [racing]);

            var results = (await CreateManager().RecommendAsync(user, new WhatToPlayRequest(), 20, CancellationToken.None)).Results;

            const string liked = "Similar to games you liked";
            const string disliked = "Similar to games you didn't enjoy";
            Assert.Contains(results.Single(r => r.GameId == cRpg).Reasons, r => r.Text == liked);
            Assert.Contains(results.Single(r => r.GameId == cPuzzle).Reasons, r => r.Text == liked);
            Assert.Contains(results.Single(r => r.GameId == cHorror).Reasons, r => r.Text == disliked);
            Assert.Contains(results.Single(r => r.GameId == cRacing).Reasons, r => r.Text == disliked);
        }
        finally
        {
            await MvpTestData.CleanupAsync(prefix);
            await ExecAsync("DELETE FROM Genres WHERE Name LIKE @p AND Id NOT IN (SELECT GenreId FROM GameGenres)", new { p = prefix + "%" });
        }
    }
}
