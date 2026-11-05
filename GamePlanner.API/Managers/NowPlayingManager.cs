using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class NowPlayingManager(GamePlannerDbContext db, IConfiguration config)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    private async Task<List<string>> GetDevicesAsync(int userId)
    {
        var devices = await db.UserDevices
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.SortOrder)
            .Select(d => d.Device)
            .ToListAsync();

        return devices.Count > 0 ? devices : [DeviceTypes.General];
    }

    public async Task<List<NowPlayingSlotDto>> GetSlotsAsync(int userId)
    {
        var devices = await GetDevicesAsync(userId);

        var rows = await db.NowPlayingGames
            .Where(n => n.UserId == userId)
            .Include(n => n.Game).ThenInclude(g => g.Genres)
            .Include(n => n.Game).ThenInclude(g => g.Tags)
            .Include(n => n.Game).ThenInclude(g => g.Themes)
            .Include(n => n.Game).ThenInclude(g => g.PlayerPerspectives)
            .Include(n => n.Game).ThenInclude(g => g.GameModes)
            .ToListAsync();

        return devices.Select(device =>
        {
            var row = rows.FirstOrDefault(r => r.Device == device);
            return new NowPlayingSlotDto
            {
                Device       = device,
                Game         = row?.Game.ToDto(),
                PlayingSince = row?.AddedAt
            };
        }).ToList();
    }

    public async Task<NowPlayingResult> StartAsync(int userId, string device, StartNowPlayingRequest r)
    {
        if (!(await GetDevicesAsync(userId)).Contains(device)) return NowPlayingResult.InvalidDevice;

        var p = new DynamicParameters(new
        {
            UserId = userId,
            Device = device,
            r.GameId,
            r.OutgoingOutcome,
            r.OutgoingRating
        });
        p.Add("Result", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.ReturnValue);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_StartNowPlaying", p, commandType: System.Data.CommandType.StoredProcedure);

        return p.Get<int>("Result") switch
        {
            0 => NowPlayingResult.Ok,
            1 => NowPlayingResult.GameNotFound,
            2 => NowPlayingResult.SlotOccupied,
            3 => NowPlayingResult.GameOnOtherDevice,
            _ => NowPlayingResult.InvalidOutcome
        };
    }

    public async Task<NowPlayingResult> RemoveAsync(int userId, string device, RemoveNowPlayingRequest r)
    {
        if (!(await GetDevicesAsync(userId)).Contains(device)) return NowPlayingResult.InvalidDevice;

        var p = new DynamicParameters(new
        {
            UserId = userId,
            Device = device,
            r.Outcome,
            r.Rating
        });
        p.Add("Result", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.ReturnValue);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_RemoveNowPlaying", p, commandType: System.Data.CommandType.StoredProcedure);

        return p.Get<int>("Result") switch
        {
            0 => NowPlayingResult.Ok,
            1 => NowPlayingResult.SlotEmpty,
            _ => NowPlayingResult.InvalidOutcome
        };
    }
}
