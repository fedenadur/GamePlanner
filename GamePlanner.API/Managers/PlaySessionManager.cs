using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class PlaySessionManager(GamePlannerDbContext db, IConfiguration config)
{
    private const int MaxNoteLength = 1000;

    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    private static void Validate(LogSessionRequest r)
    {
        if (r.Hours < 0.1m || r.Hours > 24m) throw new ArgumentException("Hours must be between 0.1 and 24.");
        if (r.Note is { Length: > MaxNoteLength }) throw new ArgumentException("Note must be 1000 characters or less.");
        if (r.PlayedOn?.Date > DateTime.UtcNow.Date) throw new ArgumentException("Date cannot be in the future.");
        if (r.Device != DeviceTypes.General && !DeviceTypes.IsSelectable(r.Device)) throw new ArgumentException("Unknown device.");
    }

    private static DynamicParameters ToParameters(int userId, LogSessionRequest r)
    {
        var p = new DynamicParameters(new
        {
            UserId   = userId,
            r.Device,
            PlayedOn = (r.PlayedOn ?? DateTime.UtcNow).Date,
            r.Hours,
            r.Note
        });
        p.Add("Result", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.ReturnValue);
        return p;
    }

    public async Task<List<PlaySessionDto>> GetForGameAsync(int userId, int gameId) =>
        await db.PlaySessions
            .Where(s => s.UserId == userId && s.GameId == gameId && db.Games.Any(g => g.Id == s.GameId && g.IsActive))
            .OrderByDescending(s => s.PlayedOn).ThenByDescending(s => s.Id)
            .Select(s => new PlaySessionDto
            {
                Id = s.Id, GameId = s.GameId, Device = s.Device, PlayedOn = s.PlayedOn, Hours = s.Hours, Note = s.Note
            })
            .ToListAsync();

    public async Task<int?> LogAsync(int userId, int gameId, LogSessionRequest request)
    {
        Validate(request);
        var p = ToParameters(userId, request);
        p.Add("GameId", gameId);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_InsertPlaySession", p, commandType: System.Data.CommandType.StoredProcedure);

        var id = p.Get<int>("Result");
        return id > 0 ? id : null;
    }

    public async Task<bool> UpdateAsync(int userId, int id, LogSessionRequest request)
    {
        Validate(request);
        var p = ToParameters(userId, request);
        p.Add("Id", id);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_UpdatePlaySession", p, commandType: System.Data.CommandType.StoredProcedure);
        return p.Get<int>("Result") > 0;
    }

    public async Task<bool> DeleteAsync(int userId, int id)
    {
        var p = new DynamicParameters(new { Id = id, UserId = userId });
        p.Add("Result", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.ReturnValue);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_DeletePlaySession", p, commandType: System.Data.CommandType.StoredProcedure);
        return p.Get<int>("Result") > 0;
    }
}
