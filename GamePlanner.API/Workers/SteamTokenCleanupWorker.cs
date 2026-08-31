using GamePlanner.API.Managers;
using GamePlanner.API.Services;

namespace GamePlanner.API.Workers;

public class SteamTokenCleanupWorker(IServiceScopeFactory scopeFactory, SteamTokenStore tokenStore, ILogger<SteamTokenCleanupWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "SteamTokenCleanupWorker";
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

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

        await using var scope = scopeFactory.CreateAsyncScope();
        var statusManager = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

        try
        {
            await statusManager.SetRunningAsync(WorkerName, ct);
            var removed = tokenStore.RemoveExpired();
            await statusManager.SetCompletedAsync(WorkerName);

            if (removed > 0)
                logger.LogInformation("{Worker}: removed {Removed} expired token(s).", WorkerName, removed);
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
