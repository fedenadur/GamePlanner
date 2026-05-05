using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameJournalManager(GamePlannerDbContext db, IConfiguration config)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public async Task<List<GameJournalEntryDto>> GetJournalAsync(int userId)
    {
        var entries = await db.GameJournalEntries
            .Where(j => j.UserId == userId)
            .Include(j => j.Game)
            .OrderByDescending(j => j.UpdatedAt)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    public async Task<GameJournalEntryDto?> GetEntryAsync(int userId, int gameId)
    {
        var entry = await db.GameJournalEntries
            .Include(j => j.Game)
            .FirstOrDefaultAsync(j => j.UserId == userId && j.GameId == gameId);

        return entry is null ? null : ToDto(entry);
    }

    public async Task UpsertAsync(int userId, int gameId, UpsertJournalRequest req)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_UpsertGameJournalEntry", new
        {
            UserId         = userId,
            GameId         = gameId,
            req.Status,
            req.StartedAt,
            req.FinishedAt,
            req.LastPlayedAt,
            req.UserRating,
            req.Notes,
            req.FavoriteMoment,
            req.ReplayCount
        }, commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task DeleteAsync(int userId, int gameId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_DeleteGameJournalEntry",
            new { UserId = userId, GameId = gameId },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    private static GameJournalEntryDto ToDto(Entities.GameJournalEntry j) => new()
    {
        GameId         = j.GameId,
        GameName       = j.Game.Name,
        CoverImageUrl  = j.Game.CoverImageUrl,
        Status         = j.Status,
        StartedAt      = j.StartedAt,
        FinishedAt     = j.FinishedAt,
        LastPlayedAt   = j.LastPlayedAt,
        UserRating     = j.UserRating,
        Notes          = j.Notes,
        FavoriteMoment = j.FavoriteMoment,
        ReplayCount    = j.ReplayCount,
        CreatedAt      = j.CreatedAt,
        UpdatedAt      = j.UpdatedAt,
        DaysToFinish   = j.StartedAt.HasValue && j.FinishedAt.HasValue
            ? (int)(j.FinishedAt.Value - j.StartedAt.Value).TotalDays
            : null
    };
}
