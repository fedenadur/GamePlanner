using GamePlanner.API.Context;
using GamePlanner.API.Entities;
using GamePlanner.API.Managers;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Workers;

public class SteamImportWorker(IServiceScopeFactory scopeFactory, ILogger<SteamImportWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("SteamImportWorker started.");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await ProcessNextPendingJobAsync(ct);
                await Task.Delay(PollInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SteamImportWorker stopped.");
        }
    }

    private async Task ProcessNextPendingJobAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<GamePlannerDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<GameImportManager>();

        // Claim the oldest pending job atomically
        var job = await db.ImportJobs
            .Where(j => j.Status == ImportJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (job is null) return;

        job.Status = ImportJobStatus.Running;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Processing import job {Id} for user {UserId}.", job.Id, job.UserId);

        try
        {
            await manager.ImportWithProgressAsync(job, ct);

            job.Status      = ImportJobStatus.Completed;
            job.Progress    = 100;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Job {Id} completed. {Count} games processed.", job.Id, job.ProcessedGames);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down — reset so it can be picked up on restart
            job.Status = ImportJobStatus.Pending;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {Id} failed.", job.Id);
            job.Status       = ImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt  = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
