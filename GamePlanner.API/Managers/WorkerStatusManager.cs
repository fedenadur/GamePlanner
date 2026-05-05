using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class WorkerStatusManager(GamePlannerDbContext db)
{
    public async Task<List<WorkerStatus>> GetAllAsync(CancellationToken ct) =>
        await db.WorkerStatuses.OrderBy(w => w.Name).ToListAsync(ct);

    public async Task SetRunningAsync(string name, CancellationToken ct)
    {
        var w = await GetOrCreateAsync(name, ct);
        w.Status    = WorkerStatusValue.Running;
        w.LastRunAt = DateTime.UtcNow;
        w.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetCompletedAsync(string name)
    {
        var w = await GetOrCreateAsync(name, CancellationToken.None);
        w.Status        = WorkerStatusValue.Completed;
        w.LastSuccessAt = DateTime.UtcNow;
        w.LastError     = null;
        await db.SaveChangesAsync();
    }

    public async Task SetFailedAsync(string name, string error)
    {
        var w = await GetOrCreateAsync(name, CancellationToken.None);
        w.Status    = WorkerStatusValue.Failed;
        w.LastError = error[..Math.Min(error.Length, 500)];
        await db.SaveChangesAsync();
    }

    public async Task SetIdleAsync(string name)
    {
        var w = await GetOrCreateAsync(name, CancellationToken.None);
        w.Status = WorkerStatusValue.Idle;
        await db.SaveChangesAsync();
    }

    private async Task<WorkerStatus> GetOrCreateAsync(string name, CancellationToken ct)
    {
        var w = await db.WorkerStatuses.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (w is not null) return w;

        w = new WorkerStatus { Name = name };
        db.WorkerStatuses.Add(w);
        return w;
    }
}
