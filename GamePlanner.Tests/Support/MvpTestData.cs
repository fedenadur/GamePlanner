// CONVENTION EXEMPTION (owner decision D-23): test-fixture writes via direct SQL, only against the _Test
// database that TestDatabase.ConnectionString has already verified.
using Dapper;
using Microsoft.Data.SqlClient;

namespace GamePlanner.Tests;

public static class MvpTestData
{
    public const string Prefix = "mvp-probe-";

    public static async Task<int> CreateUserAsync(string prefix)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Users (Email) OUTPUT INSERTED.Id VALUES (@email)",
            new { email = $"{prefix}{Guid.NewGuid():N}@test.local" });
    }

    public static async Task<int> CreateGameAsync(int userId, string name, string store = "Other",
        string[]? genres = null, double steamHours = 0, int estimatedMinutes = 0)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO Games (Name, EstimatedMinutes, IsActive, UserId, Store, PlaytimeHours)
            OUTPUT INSERTED.Id
            VALUES (@name, @estimatedMinutes, 1, @userId, @store, @steamHours)
            """,
            new { name, estimatedMinutes, userId, store, steamHours });

        foreach (var genre in genres ?? [])
        {
            await conn.ExecuteAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = @genre) INSERT INTO Genres (Name) VALUES (@genre);
                INSERT INTO GameGenres (GameId, GenreId) SELECT @id, Id FROM Genres WHERE Name = @genre;
                """,
                new { genre, id });
        }

        return id;
    }

    public static async Task SetDevicesAsync(int userId, params string[] devices)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM UserDevices WHERE UserId = @userId", new { userId });
        for (var i = 0; i < devices.Length; i++)
            await conn.ExecuteAsync(
                "INSERT INTO UserDevices (UserId, Device, SortOrder) VALUES (@userId, @device, @i)",
                new { userId, device = devices[i], i });
    }

    /// <summary>Hard-deletes every user whose email starts with the prefix, plus their games and dependent rows.</summary>
    public static async Task CleanupAsync(string prefix)
    {
        await using var conn = new SqlConnection(TestDatabase.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            DECLARE @users TABLE (Id INT);
            INSERT INTO @users SELECT Id FROM Users WHERE Email LIKE @p;
            DECLARE @games TABLE (Id INT);
            INSERT INTO @games SELECT Id FROM Games WHERE UserId IN (SELECT Id FROM @users) OR Name LIKE @p;

            DELETE FROM PlaySessions        WHERE UserId IN (SELECT Id FROM @users) OR GameId IN (SELECT Id FROM @games);
            DELETE FROM NowPlayingGames     WHERE UserId IN (SELECT Id FROM @users) OR GameId IN (SELECT Id FROM @games);
            DELETE FROM GameJournalEntries  WHERE UserId IN (SELECT Id FROM @users) OR GameId IN (SELECT Id FROM @games);
            DELETE FROM UserDevices         WHERE UserId IN (SELECT Id FROM @users);
            DELETE FROM UserPreferenceGenres WHERE UserId IN (SELECT Id FROM @users);
            DELETE FROM UserPreferences     WHERE UserId IN (SELECT Id FROM @users);
            DELETE FROM GamePlayableDevices WHERE GameId IN (SELECT Id FROM @games);
            DELETE FROM GameGenres          WHERE GameId IN (SELECT Id FROM @games);
            DELETE FROM Games               WHERE Id IN (SELECT Id FROM @games);
            DELETE FROM Users               WHERE Id IN (SELECT Id FROM @users);
            """,
            new { p = prefix + "%" });
    }
}
