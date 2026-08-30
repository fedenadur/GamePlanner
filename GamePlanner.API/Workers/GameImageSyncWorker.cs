using GamePlanner.API.Managers;

namespace GamePlanner.API.Workers;

public class GameImageSyncWorker(IServiceScopeFactory scopeFactory, ILogger<GameImageSyncWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "GameImageSyncWorker";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("{Worker} started.", WorkerName);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RunAsync(ct);
                await Task.Delay(Interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{Worker} stopped.", WorkerName);
        }
    }

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("{Worker} triggered at {Time}.", WorkerName, DateTime.UtcNow);

        await using var scope  = scopeFactory.CreateAsyncScope();
        var manager            = scope.ServiceProvider.GetRequiredService<GameImageManager>();
        var statusManager      = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

        try
        {
            await statusManager.SetRunningAsync(WorkerName, ct);
            var (failedCount, failureDetail) = await manager.RunAsync(ct);
            await statusManager.SetCompletedAsync(WorkerName, failedCount, failureDetail);

            if (failedCount > 0)
                logger.LogWarning("{Worker}: {FailedCount} item(s) failed.", WorkerName, failedCount);
        }
        catch (OperationCanceledException)
        {
            await statusManager.SetIdleAsync(WorkerName);
            logger.LogWarning("{Worker} was cancelled.", WorkerName);
        }
        catch (Exception ex)
        {
            await statusManager.SetFailedAsync(WorkerName, ex.Message);
            logger.LogError(ex, "{Worker} failed.", WorkerName);
        }
    }
}
