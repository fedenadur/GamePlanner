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
        var hours = decimal.Round(r.Hours, 2);
        if (hours < 0.1m || hours > 24m) throw new ArgumentException("Hours must be between 0.1 and 24.");
        if (r.Note is { Length: > MaxNoteLength }) throw new ArgumentException("Note must be 1000 characters or less.");
        if (r.PlayedOn?.Date > DateTime.UtcNow.Date) throw new ArgumentException("Date cannot be in the future.");
    }

    // The device must be one of the user's devices, or General when the user has none.
    // keepDevice: an edited session may keep the device it already has, even if the user removed it since.
    private async Task ValidateDeviceAsync(int userId, string? device, string? keepDevice = null)
    {
        if (device is null) throw new ArgumentException("Unknown device.");
        if (device == keepDevice) return;
        var devices = await db.UserDevices.Where(d => d.UserId == userId).Select(d => d.Device).ToListAsync();
        var valid = devices.Count == 0 ? device == DeviceTypes.General : devices.Contains(device);
        if (!valid) throw new ArgumentException("Unknown device.");
    }

    private static DynamicParameters ToParameters(int userId, LogSessionRequest r)
    {
        var p = new DynamicParameters(new
        {
            UserId   = userId,
            r.Device,
            PlayedOn = (r.PlayedOn ?? DateTime.UtcNow).Date,
            Hours    = decimal.Round(r.Hours, 2),
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
        await ValidateDeviceAsync(userId, request.Device);
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
        var current = await db.PlaySessions.Where(s => s.Id == id && s.UserId == userId).Select(s => s.Device).FirstOrDefaultAsync();
        if (current is null) return false;
        await ValidateDeviceAsync(userId, request.Device, current);
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
