using GamePlanner.API.Managers;

namespace GamePlanner.API.Workers;

public class GameSyncWorker(IServiceScopeFactory scopeFactory, ILogger<GameSyncWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "GameSyncWorker";
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
        var manager            = scope.ServiceProvider.GetRequiredService<GameSyncManager>();
        var statusManager      = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

        try
        {
            await statusManager.SetRunningAsync(WorkerName, ct);
            await manager.RunAsync(ct);
            await statusManager.SetCompletedAsync(WorkerName);
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
