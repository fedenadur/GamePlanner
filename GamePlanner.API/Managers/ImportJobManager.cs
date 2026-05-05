using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class ImportJobManager(GamePlannerDbContext db)
{
    public async Task<ImportJob> CreateAsync(int userId, string steamId, CancellationToken ct)
    {
        var job = new ImportJob { UserId = userId, SteamId = steamId };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<ImportJob?> GetForUserAsync(int jobId, int userId, CancellationToken ct) =>
        await db.ImportJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, ct);

    public async Task<ImportJob?> GetActivePendingAsync(int userId, CancellationToken ct) =>
        await db.ImportJobs
            .Where(j => j.UserId == userId &&
                   (j.Status == ImportJobStatus.Pending || j.Status == ImportJobStatus.Running))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
