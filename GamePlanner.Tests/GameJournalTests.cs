// CONVENTION EXEMPTION (owner decision D-23): arrange helpers use direct SQL against the _Test database.
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
public class GameJournalTests
{
    private static string NewPrefix() => $"{MvpTestData.Prefix}jr-{Guid.NewGuid():N}-";

    private static GameJournalManager CreateManager() => new(TestDatabase.CreateContext(), TestDatabase.CreateConfig());

    private static GameJournalController CreateController(int userId) => new(CreateManager())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
            }
        }
    };

    private static async Task<int> CountEntriesAsync(int user, int game)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GameJournalEntries WHERE UserId=@user AND GameId=@game", new { user, game });
    }

    [Fact]
    public async Task GetJournal_ExcludesDeletedGames()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var kept = await MvpTestData.CreateGameAsync(user, prefix + "kept");
            var deleted = await MvpTestData.CreateGameAsync(user, prefix + "deleted");
            var manager = CreateManager();
            await manager.UpsertAsync(user, kept, new UpsertJournalRequest { Status = JournalStatuses.Playing });
            await manager.UpsertAsync(user, deleted, new UpsertJournalRequest { Status = JournalStatuses.Finished });
            await using (var conn = new SqlConnection(TestDatabase.ConnectionString))
                await conn.ExecuteAsync("UPDATE Games SET IsActive = 0 WHERE Id = @deleted", new { deleted });

            var entry = Assert.Single(await CreateManager().GetJournalAsync(user));
            Assert.Equal(kept, entry.GameId);
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Theory]
    [InlineData("Nope", null)]
    [InlineData("playing", null)]
    [InlineData(null, null)]
    [InlineData("Playing", 11)]
    public async Task Upsert_InvalidStatusOrRating_Returns400_AndWritesNothing(string? status, int? rating)
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");
            var request = new UpsertJournalRequest { Status = status!, UserRating = (byte?)rating };

            Assert.IsType<BadRequestObjectResult>(await CreateController(user).CreateEntry(game, request));
            Assert.IsType<BadRequestObjectResult>(await CreateController(user).UpdateEntry(game, request));
            Assert.Equal(0, await CountEntriesAsync(user, game));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }

    [Fact]
    public async Task Upsert_ValidStatus_Created()
    {
        var prefix = NewPrefix();
        try
        {
            var user = await MvpTestData.CreateUserAsync(prefix);
            var game = await MvpTestData.CreateGameAsync(user, prefix + "g");

            var result = await CreateController(user).CreateEntry(game, new UpsertJournalRequest { Status = JournalStatuses.Paused });

            Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(1, await CountEntriesAsync(user, game));
        }
        finally { await MvpTestData.CleanupAsync(prefix); }
    }
}
