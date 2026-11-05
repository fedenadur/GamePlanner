// CONVENTION EXEMPTION (owner decision D-23): EnsureUserAsync (INSERT), InsertProbeGameAsync (UPDATE after
// sp_InsertGame), PurgeProbeRowsAsync (DELETE), the Steam test cleanup and the plain arrange UPDATEs in the test
// classes are test-fixture writes. They reach only the database that ConnectionString has already checked for the
// _Test suffix. Production code keeps the stored-procedure-only write rule.
using Dapper;
using GamePlanner.API.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GamePlanner.Tests;

public static class TestDatabase
{
    public const string CollectionName = "GamePlannerDB_Test";
    public const string UnconfiguredCatalog = "TestDbGuard_Unconfigured";

    private const string EnvVar = "ConnectionStrings__DefaultConnection";

    private static string? ReadTestConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var catalog = new SqlConnectionStringBuilder(cs).InitialCatalog;
        return catalog.EndsWith("_Test", StringComparison.OrdinalIgnoreCase) ? cs : null;
    }

    /// <summary>The _Test database connection string. Throws when the env var is missing or names any other database.</summary>
    public static string ConnectionString =>
        ReadTestConnectionString()
        ?? throw new InvalidOperationException(
            "Refusing to run: ConnectionStrings__DefaultConnection must name a database ending in _Test (build it with scripts/build-test-db.ps1)");

    /// <summary>Connection string for the in-process API host. Never throws; falls back to a catalog that does not exist.</summary>
    public static string ApiHostConnectionString =>
        ReadTestConnectionString()
        ?? $"Server=localhost;Database={UnconfiguredCatalog};Trusted_Connection=True;TrustServerCertificate=True;";

    public static GamePlannerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GamePlannerDbContext>().UseSqlServer(ConnectionString).Options);

    public static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["IGDB:ClientId"] = ApiFactory.TestIgdbClientId,
                ["IGDB:ClientSecret"] = ApiFactory.TestIgdbSecret
            })
            .Build();

    public static async Task<int> EnsureUserAsync(string email)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<int?>("SELECT Id FROM Users WHERE Email = @email", new { email });
        if (id is not null) return id.Value;
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Users (Email) OUTPUT INSERTED.Id VALUES (@email)", new { email });
    }

    public static async Task<int> InsertProbeGameAsync(string name, int userId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<int>("sp_InsertGame",
            new { Name = name, EstimatedMinutes = 60 },
            commandType: System.Data.CommandType.StoredProcedure);
        await conn.ExecuteAsync("UPDATE Games SET UserId = @userId, IsActive = 1 WHERE Id = @id", new { userId, id });
        return id;
    }

    public static async Task PurgeProbeRowsAsync(string namePrefix)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            DELETE FROM GameImages             WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GameGenres             WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GameTags               WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GameThemes             WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GamePlayerPerspectives WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM GameModesRelation      WHERE GameId IN (SELECT Id FROM Games WHERE Name LIKE @Prefix);
            DELETE FROM Games                  WHERE Name LIKE @Prefix;
            """,
            new { Prefix = namePrefix + "%" });
    }
}

[CollectionDefinition(TestDatabase.CollectionName, DisableParallelization = true)]
public class TestDatabaseCollection { }
