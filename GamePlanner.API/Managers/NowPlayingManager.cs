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

    public async Task<List<NowPlayingSlotDto>> GetSlotsAsync(int userId)
    {
        var rows = await db.NowPlayingGames
            .Where(n => n.UserId == userId)
            .Include(n => n.Game).ThenInclude(g => g.Genres)
            .Include(n => n.Game).ThenInclude(g => g.Tags)
            .Include(n => n.Game).ThenInclude(g => g.Themes)
            .Include(n => n.Game).ThenInclude(g => g.PlayerPerspectives)
            .Include(n => n.Game).ThenInclude(g => g.GameModes)
            .ToListAsync();

        return Enumerable.Range(0, 3).Select(slot =>
        {
            var row = rows.FirstOrDefault(r => r.Slot == slot);
            return new NowPlayingSlotDto
            {
                Slot = slot,
                Game = row?.Game.ToDto()
            };
        }).ToList();
    }

    public async Task SetSlotAsync(int userId, int slot, int gameId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_UpsertNowPlayingGame",
            new { UserId = userId, GameId = gameId, Slot = slot },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task ClearSlotAsync(int userId, int slot)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_DeleteNowPlayingGame",
            new { UserId = userId, Slot = slot },
            commandType: System.Data.CommandType.StoredProcedure);
    }
}
