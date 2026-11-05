using Dapper;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using GamePlanner.API.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GamePlanner.API.Managers;

public class GameManager(GamePlannerDbContext db, IConfiguration config)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));

    public async Task<List<GameDto>> GetAllAsync(int userId)
    {
        var games = await db.Games
            .Where(g => g.IsActive && g.UserId == userId)
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .Include(g => g.PlayableDevices)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var journal = await LoadJournalStatusesAsync(userId, games.Select(g => g.Id).ToList());
        var sessions = await LoadSessionTotalsAsync(userId);

        return games.Select(g => ToLibraryDto(g, journal, sessions)).ToList();
    }

    public async Task<GameDto?> GetByIdAsync(int id, int userId)
    {
        var game = await db.Games
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .Include(g => g.PlayableDevices)
            .FirstOrDefaultAsync(g => g.Id == id && g.IsActive && g.UserId == userId);

        if (game is null) return null;

        var journal = await LoadJournalStatusesAsync(userId, [game.Id]);
        var sessions = await LoadSessionTotalsAsync(userId, game.Id);
        return ToLibraryDto(game, journal, sessions);
    }

    public async Task<GameDetailDto?> GetDetailAsync(int id, int userId)
    {
        var game = await db.Games
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .Include(g => g.PlayableDevices)
            .FirstOrDefaultAsync(g => g.Id == id && g.IsActive && g.UserId == userId);

        if (game is null) return null;

        // Find HLTB data: prefer SteamAppId match, fallback to Name
        Entities.GameHLTB? hltb = null;
        if (game.SteamAppId.HasValue)
            hltb = await db.GamesHLTB.FirstOrDefaultAsync(h => h.SteamAppId == game.SteamAppId.Value);

        hltb ??= await db.GamesHLTB
            .FirstOrDefaultAsync(h => h.Name.ToLower() == game.Name.ToLower());

        var journal = await LoadJournalStatusesAsync(userId, [game.Id]);
        var sessions = await LoadSessionTotalsAsync(userId, game.Id);
        var base_ = ToLibraryDto(game, journal, sessions);

        return new GameDetailDto
        {
            Id                 = base_.Id,
            Name               = base_.Name,
            EstimatedMinutes   = base_.EstimatedMinutes,
            Genres             = base_.Genres,
            Tags               = base_.Tags,
            Themes             = base_.Themes,
            PlayerPerspectives = base_.PlayerPerspectives,
            GameModes          = base_.GameModes,
            CoverImageUrl      = base_.CoverImageUrl,
            PlaytimeHours      = base_.PlaytimeHours,
            Source             = base_.Source,
            Store              = base_.Store,
            IsFavorite         = base_.IsFavorite,
            PlayableDevices    = base_.PlayableDevices,
            Status             = base_.Status,
            LoggedHours        = base_.LoggedHours,
            SessionCount       = base_.SessionCount,
            TotalHours         = base_.TotalHours,
            ReleaseYear        = base_.ReleaseYear,
            IGDBId             = game.IGDBId,
            SteamAppId         = game.SteamAppId,
            SteamUrl           = game.SteamAppId.HasValue
                                 ? $"https://store.steampowered.com/app/{game.SteamAppId.Value}"
                                 : null,
            HltbMainStory          = hltb?.MainStoryHours,
            HltbMainPlus           = hltb?.MainPlusHours,
            HltbCompletionist      = hltb?.CompletionistHours,
            AggregatedRating       = game.AggregatedRating,
            AggregatedRatingCount  = game.AggregatedRatingCount,
        };
    }

    private async Task<Dictionary<int, string>> LoadJournalStatusesAsync(int userId, List<int> gameIds) =>
        await db.GameJournalEntries
            .Where(j => j.UserId == userId && gameIds.Contains(j.GameId))
            .ToDictionaryAsync(j => j.GameId, j => j.Status);

    private async Task<Dictionary<int, (double Hours, int Count)>> LoadSessionTotalsAsync(int userId, int? gameId = null)
    {
        var rows = await db.PlaySessions
            .Where(p => p.UserId == userId && (gameId == null || p.GameId == gameId))
            .GroupBy(p => p.GameId)
            .Select(x => new { GameId = x.Key, Hours = x.Sum(p => p.Hours), Count = x.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.GameId, r => ((double)r.Hours, r.Count));
    }

    private static GameDto ToLibraryDto(Game game, Dictionary<int, string> journal, Dictionary<int, (double Hours, int Count)> sessions)
    {
        var (hours, count) = sessions.GetValueOrDefault(game.Id);
        return game.ToDto(journal.GetValueOrDefault(game.Id, JournalStatuses.Backlog), hours, count);
    }

    private static void ValidateStoreAndDevices(string store, List<string> devices)
    {
        if (!Stores.All.Contains(store))
            throw new ArgumentException($"Invalid store '{store}'.");
        if (devices.Any(d => !DeviceTypes.IsSelectable(d)) || devices.Distinct().Count() != devices.Count)
            throw new ArgumentException("Invalid or duplicate playable device.");
    }

    public async Task<int> CreateFromIgdbAsync(CreateGameFromIgdbRequest request, int userId)
    {
        ValidateStoreAndDevices(request.Store, []);
        var existing = await db.Games
            .FirstOrDefaultAsync(g => g.IGDBId == request.IgdbId && g.UserId == userId && g.IsActive);

        if (existing is not null)
            return existing.Id;

        await using var tx = await db.Database.BeginTransactionAsync();
        var conn = db.Database.GetDbConnection();
        var newId = await conn.ExecuteScalarAsync<int>("sp_InsertGame",
            new { request.Name, request.EstimatedMinutes },
            transaction: tx.GetDbTransaction(),
            commandType: System.Data.CommandType.StoredProcedure);

        var game = await db.Games
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .FirstAsync(g => g.Id == newId);

        game.UserId = userId;
        game.IGDBId = request.IgdbId;
        game.CoverImageUrl = request.CoverUrl;
        game.Store = request.Store;

        await SyncGenresAsync(game, request.Genres);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return newId;
    }

    public async Task<int> CreateAsync(CreateGameRequest request, int userId)
    {
        ValidateStoreAndDevices(request.Store, []);
        await using var tx = await db.Database.BeginTransactionAsync();
        var conn = db.Database.GetDbConnection();
        var newId = await conn.ExecuteScalarAsync<int>("sp_InsertGame",
            new { request.Name, request.EstimatedMinutes },
            transaction: tx.GetDbTransaction(),
            commandType: System.Data.CommandType.StoredProcedure);

        var game = await db.Games
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .FirstAsync(g => g.Id == newId);

        game.UserId = userId;
        game.Store = request.Store;
        await SyncGenresAsync(game, request.Genres);
        await SyncTagsAsync(game, request.Tags);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return newId;
    }

    public async Task<bool> UpdateAsync(int id, UpdateGameRequest request, int userId)
    {
        ValidateStoreAndDevices(request.Store, request.PlayableDevices);

        var game = await db.Games
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Themes)
            .Include(g => g.PlayerPerspectives)
            .Include(g => g.GameModes)
            .FirstOrDefaultAsync(g => g.Id == id && g.IsActive && g.UserId == userId);

        if (game is null) return false;

        await using var tx = await db.Database.BeginTransactionAsync();
        var conn = db.Database.GetDbConnection();
        await conn.ExecuteAsync("sp_UpdateGame",
            new { Id = id, request.Name, request.EstimatedMinutes, request.Store, request.IsFavorite, request.CoverImageUrl },
            transaction: tx.GetDbTransaction(),
            commandType: System.Data.CommandType.StoredProcedure);

        await conn.ExecuteAsync("sp_SetGamePlayableDevices",
            new { GameId = id, Devices = string.Join(',', request.PlayableDevices) },
            transaction: tx.GetDbTransaction(),
            commandType: System.Data.CommandType.StoredProcedure);

        await SyncGenresAsync(game, request.Genres);
        await SyncTagsAsync(game, request.Tags);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return true;
    }

    public async Task<bool> SetFavoriteAsync(int id, bool isFavorite, int userId)
    {
        using var conn = CreateConnection();
        var rows = await conn.ExecuteScalarAsync<int>("sp_SetGameFavorite",
            new { Id = id, UserId = userId, IsFavorite = isFavorite },
            commandType: System.Data.CommandType.StoredProcedure);

        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var exists = await db.Games.AnyAsync(g => g.Id == id && g.IsActive && g.UserId == userId);
        if (!exists) return false;

        using var conn = CreateConnection();
        var rows = await conn.ExecuteScalarAsync<int>("sp_DeleteGame",
            new { Id = id },
            commandType: System.Data.CommandType.StoredProcedure);

        return rows > 0;
    }

    private async Task SyncGenresAsync(Game game, List<string> names)
    {
        game.Genres.Clear();
        foreach (var name in names.Select(n => n.Trim()).Where(n => n.Length > 0))
        {
            var genre = await db.Genres.FirstOrDefaultAsync(g => g.Name == name)
                        ?? db.Genres.Add(new Genre { Name = name }).Entity;
            game.Genres.Add(genre);
        }
    }

    private async Task SyncTagsAsync(Game game, List<string> names)
    {
        game.Tags.Clear();
        foreach (var name in names.Select(n => n.Trim().ToLower()).Where(n => n.Length > 0))
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name)
                      ?? db.Tags.Add(new Tag { Name = name }).Entity;
            game.Tags.Add(tag);
        }
    }
}
