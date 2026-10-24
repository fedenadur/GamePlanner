using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GamePlanner.Tests;

/// <summary>
/// Proves, against a real SQL Server instance, that GameManager's write paths are atomic
/// (a mid-write failure rolls the stored-procedure write back with the EF write) and that
/// ModifiedAt/DeletedAt are stamped exclusively in T-SQL. Every throwaway row created here
/// carries the "gsd-probe-" name prefix and is removed in a finally block.
/// </summary>
public class GameManagerAtomicityTests
{
    private const string NamePrefix = "gsd-probe-";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;";

    private static GamePlannerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GamePlannerDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new GamePlannerDbContext(options);
    }

    private static GameManager CreateManager(GamePlannerDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();

        return new GameManager(db, config);
    }

    /// <summary>Hard-deletes every probe row (and its junction rows) whose Name starts with the given prefix.</summary>
    private static async Task CleanupByNamePrefixAsync(string namePrefix)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            DELETE FROM GameGenres WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GameTags   WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM Games      WHERE Name LIKE @Prefix;
            """,
            new { Prefix = namePrefix + "%" });
    }

    [Fact]
    public async Task UpdateAsync_RollsBackStoredProcWrite_WhenSaveChangesThrows()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        var gameId = await manager.CreateAsync(
            new CreateGameRequest { Name = name, EstimatedMinutes = 60, Genres = [], Tags = [] },
            userId);

        try
        {
            var before = await db.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

            // Genres.Name is NVARCHAR(100); a 150-char genre forces a truncation DbUpdateException
            // from SaveChangesAsync, *after* sp_UpdateGame has already run inside the transaction.
            var overLengthGenre = new string('x', 150);

            await Assert.ThrowsAsync<DbUpdateException>(() => manager.UpdateAsync(
                gameId,
                new UpdateGameRequest
                {
                    Name = name + "-updated",
                    EstimatedMinutes = 999,
                    Genres = [overLengthGenre],
                    Tags = []
                },
                userId));

            // Fresh context = fresh connection, so this read cannot see the aborted transaction's state.
            await using var freshDb = CreateContext();
            var after = await freshDb.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

            Assert.Equal(before.Name, after.Name);
            Assert.Equal(before.EstimatedMinutes, after.EstimatedMinutes);
            Assert.Equal(before.ModifiedAt, after.ModifiedAt);
        }
        finally
        {
            await CleanupByNamePrefixAsync(name);
        }
    }

    [Fact]
    public async Task UpdateAsync_StampsModifiedAt_OnSuccess()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        var gameId = await manager.CreateAsync(
            new CreateGameRequest { Name = name, EstimatedMinutes = 30, Genres = [], Tags = [] },
            userId);

        try
        {
            var ok = await manager.UpdateAsync(
                gameId,
                new UpdateGameRequest
                {
                    Name = name + "-v2",
                    EstimatedMinutes = 45,
                    Genres = ["Action"],
                    Tags = []
                },
                userId);

            Assert.True(ok);

            await using var freshDb = CreateContext();
            var after = await freshDb.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

            Assert.NotNull(after.ModifiedAt);
            Assert.True(Math.Abs((DateTime.UtcNow - after.ModifiedAt!.Value).TotalSeconds) < 60);
        }
        finally
        {
            await CleanupByNamePrefixAsync(name);
        }
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotent_SecondCallIsNoOp()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        var gameId = await manager.CreateAsync(
            new CreateGameRequest { Name = name, EstimatedMinutes = 10, Genres = [], Tags = [] },
            userId);

        try
        {
            var deleted = await manager.DeleteAsync(gameId, userId);
            Assert.True(deleted);

            await using var readDb = CreateContext();
            var afterFirst = await readDb.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);
            Assert.NotNull(afterFirst.DeletedAt);

            // Call sp_DeleteGame a second time directly, bypassing the manager's exists-check,
            // to prove the proc's own IsActive = 1 guard makes a repeat delete a no-op.
            await using var conn = new SqlConnection(ConnectionString);
            var rowsAffected = await conn.ExecuteScalarAsync<int>("sp_DeleteGame",
                new { Id = gameId },
                commandType: System.Data.CommandType.StoredProcedure);

            Assert.Equal(0, rowsAffected);

            await using var readDb2 = CreateContext();
            var afterSecond = await readDb2.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);
            Assert.Equal(afterFirst.DeletedAt, afterSecond.DeletedAt);
        }
        finally
        {
            await CleanupByNamePrefixAsync(name);
        }
    }

    [Fact]
    public async Task CreateAsync_RollsBackInsertedGame_WhenSaveChangesThrows()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        try
        {
            // Same over-length-genre truncation trick as the update rollback fact, but here it
            // fires against a brand-new sp_InsertGame row: the insert must not survive un-rolled-back.
            var overLengthGenre = new string('x', 150);

            await Assert.ThrowsAsync<DbUpdateException>(() => manager.CreateAsync(
                new CreateGameRequest
                {
                    Name = name,
                    EstimatedMinutes = 20,
                    Genres = [overLengthGenre],
                    Tags = []
                },
                userId));

            await using var freshDb = CreateContext();
            var survived = await freshDb.Games.AnyAsync(g => g.Name == name);
            Assert.False(survived);
        }
        finally
        {
            // Cleanup still runs by prefix even though the row should never have committed --
            // if the assertion above ever fails, a partially-committed row is still removed.
            await CleanupByNamePrefixAsync(name);
        }
    }

    [Fact]
    public async Task CreateFromIgdbAsync_RollsBackInsertedGame_WhenSaveChangesThrows()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";
        var igdbId = 9_000_000_000L + Random.Shared.Next(1, 1_000_000);

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        try
        {
            // Over-length genre fires the truncation DbUpdateException after sp_InsertGame ran
            // inside the shared transaction; the inserted Games row must roll back with it.
            var overLengthGenre = new string('x', 150);

            await Assert.ThrowsAsync<DbUpdateException>(() => manager.CreateFromIgdbAsync(
                new CreateGameFromIgdbRequest
                {
                    IgdbId = igdbId,
                    Name = name,
                    EstimatedMinutes = 20,
                    Genres = [overLengthGenre]
                },
                userId));

            await using var freshDb = CreateContext();
            var survived = await freshDb.Games.AnyAsync(g => g.Name == name);
            Assert.False(survived);
        }
        finally
        {
            await CleanupByNamePrefixAsync(name);
        }
    }

    [Fact]
    public async Task CreateFromIgdbAsync_ReturnsExistingId_WhenIgdbIdAlreadyLinkedToUser()
    {
        var name = $"{NamePrefix}{Guid.NewGuid():N}";
        var igdbId = Random.Shared.NextInt64(1_000_000_000, 2_000_000_000);

        await using var db = CreateContext();
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        var manager = CreateManager(db);

        var firstId = await manager.CreateFromIgdbAsync(
            new CreateGameFromIgdbRequest { IgdbId = igdbId, Name = name, EstimatedMinutes = 25, Genres = [] },
            userId);

        try
        {
            var secondId = await manager.CreateFromIgdbAsync(
                new CreateGameFromIgdbRequest { IgdbId = igdbId, Name = name, EstimatedMinutes = 25, Genres = [] },
                userId);

            Assert.Equal(firstId, secondId);

            await using var freshDb = CreateContext();
            var count = await freshDb.Games.CountAsync(g => g.IGDBId == igdbId && g.UserId == userId);
            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupByNamePrefixAsync(name);
        }
    }
}
