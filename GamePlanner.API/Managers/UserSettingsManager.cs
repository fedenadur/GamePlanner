using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class UserSettingsManager(GamePlannerDbContext db, IConfiguration config)
{
    private static readonly string[] PreferenceValues = ["Prefer", "Neutral", "Avoid"];

    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public Task<List<string>> GetDevicesAsync(int userId) =>
        db.UserDevices.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.SortOrder)
            .Select(d => d.Device)
            .ToListAsync();

    public async Task<SaveDevicesResult> SaveDevicesAsync(int userId, List<string> devices)
    {
        if (devices.Any(d => string.IsNullOrWhiteSpace(d) || !DeviceTypes.IsSelectable(d)) || devices.Distinct().Count() != devices.Count)
            return SaveDevicesResult.Invalid;

        var p = new DynamicParameters(new { UserId = userId, Devices = string.Join(',', devices) });
        p.Add("Result", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.ReturnValue);

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_SaveUserDevices", p, commandType: System.Data.CommandType.StoredProcedure);
        return p.Get<int>("Result") == 0 ? SaveDevicesResult.Ok : SaveDevicesResult.DeviceInUse;
    }

    public async Task<UserPreferencesDto> GetPreferencesAsync(int userId)
    {
        var pref = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var genres = await db.UserPreferenceGenres.AsNoTracking()
            .Where(g => g.UserId == userId)
            .Join(db.Genres, g => g.GenreId, genre => genre.Id, (g, genre) => new { g.Kind, genre.Name })
            .OrderBy(x => x.Name)
            .ToListAsync();

        var dto = new UserPreferencesDto
        {
            FavoriteGenres = genres.Where(g => g.Kind == "Favorite").Select(g => g.Name).ToList(),
            AvoidGenres    = genres.Where(g => g.Kind == "Avoid").Select(g => g.Name).ToList()
        };
        if (pref is not null)
        {
            dto.MaxGameHours = pref.MaxGameHours;
            dto.SinglePlayer = pref.SinglePlayer;
            dto.Narrative    = pref.Narrative;
            dto.Combat       = pref.Combat;
        }
        return dto;
    }

    public async Task<bool> SavePreferencesAsync(int userId, UserPreferencesDto p)
    {
        if (p.MaxGameHours is < 1 or > 500) return false;
        if (!PreferenceValues.Contains(p.SinglePlayer) || !PreferenceValues.Contains(p.Narrative)
            || !PreferenceValues.Contains(p.Combat)) return false;
        if (p.FavoriteGenres.Concat(p.AvoidGenres).Any(g => string.IsNullOrWhiteSpace(g) || g.Contains('|'))) return false;
        if (p.FavoriteGenres.Intersect(p.AvoidGenres, StringComparer.OrdinalIgnoreCase).Any()) return false;

        using var conn = CreateConnection();
        await conn.ExecuteAsync("sp_SaveUserPreferences",
            new
            {
                UserId = userId,
                p.MaxGameHours,
                p.SinglePlayer,
                p.Narrative,
                p.Combat,
                FavoriteGenres = string.Join('|', p.FavoriteGenres),
                AvoidGenres    = string.Join('|', p.AvoidGenres)
            },
            commandType: System.Data.CommandType.StoredProcedure);
        return true;
    }

    public Task<List<string>> GetUserGenresAsync(int userId) =>
        db.Games
            .Where(g => g.IsActive && g.UserId == userId)
            .SelectMany(g => g.Genres)
            .Select(g => g.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
}
