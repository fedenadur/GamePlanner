using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class GameHLTBManager(GamePlannerDbContext db, ILogger<GameHLTBManager> logger)
{
    private const int BatchSize = 200;

    public async Task UpsertAsync(List<HLTBGameDto> records, CancellationToken ct)
    {
        if (records.Count == 0) return;

        int inserted = 0, updated = 0, unchanged = 0;

        // Build in-memory lookup of existing records (SteamAppId and Name)
        // Only load keys — avoid loading the full dataset
        var allExisting = await db.GamesHLTB.ToListAsync(ct);

        var existingBySteamId = allExisting
            .Where(g => g.SteamAppId != null)
            .GroupBy(g => g.SteamAppId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var existingByName = allExisting
            .GroupBy(g => g.Name.ToLower())
            .ToDictionary(g => g.Key, g => g.First());

        for (int i = 0; i < records.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = records.Skip(i).Take(BatchSize);

            foreach (var dto in batch)
            {
                // Match: prefer SteamAppId, fallback to Name
                GameHLTB? existing = null;

                if (dto.SteamAppId.HasValue)
                    existingBySteamId.TryGetValue(dto.SteamAppId.Value, out existing);

                existing ??= existingByName.GetValueOrDefault(dto.Name.ToLower());

                if (existing is null)
                {
                    var entity = new GameHLTB
                    {
                        Name               = dto.Name,
                        SteamAppId         = dto.SteamAppId,
                        MainStoryHours     = dto.MainStoryHours,
                        MainPlusHours      = dto.MainPlusHours,
                        CompletionistHours = dto.CompletionistHours,
                        LastUpdated        = DateTime.UtcNow
                    };
                    db.GamesHLTB.Add(entity);

                    // Keep lookups current for duplicates within the same CSV
                    if (dto.SteamAppId.HasValue) existingBySteamId[dto.SteamAppId.Value] = entity;
                    existingByName[dto.Name.ToLower()] = entity;

                    inserted++;
                }
                else if (HasChanged(existing, dto))
                {
                    existing.Name               = dto.Name;
                    existing.SteamAppId         = dto.SteamAppId ?? existing.SteamAppId;
                    existing.MainStoryHours     = dto.MainStoryHours;
                    existing.MainPlusHours      = dto.MainPlusHours;
                    existing.CompletionistHours = dto.CompletionistHours;
                    existing.LastUpdated        = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogDebug("HLTB batch {Start}–{End} processed.", i, i + BatchSize);
        }

        logger.LogInformation(
            "HLTB upsert complete: {Inserted} inserted, {Updated} updated, {Unchanged} unchanged.",
            inserted, updated, unchanged);
    }

    private static bool HasChanged(GameHLTB existing, HLTBGameDto dto) =>
        existing.MainStoryHours     != dto.MainStoryHours     ||
        existing.MainPlusHours      != dto.MainPlusHours      ||
        existing.CompletionistHours != dto.CompletionistHours ||
        (dto.SteamAppId.HasValue && existing.SteamAppId != dto.SteamAppId);
}
