using GamePlanner.API.Managers;
using GamePlanner.API.Services;

namespace GamePlanner.API.Workers;

public class UpcomingGamesSyncWorker(IServiceScopeFactory scopeFactory, ILogger<UpcomingGamesSyncWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "UpcomingGamesSyncWorker";
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

        await using var scope   = scopeFactory.CreateAsyncScope();
        var igdb                = scope.ServiceProvider.GetRequiredService<IgdbService>();
        var manager             = scope.ServiceProvider.GetRequiredService<UpcomingGamesManager>();
        var statusManager       = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

        try
        {
            await statusManager.SetRunningAsync(WorkerName, ct);

            var start = DateTimeOffset.UtcNow;
            var start1 = start.AddDays(-7);
            var end   = start.AddMonths(3);

            var games = await igdb.FetchUpcomingAsync(start1, end, ct);
            logger.LogInformation("{Worker} fetched {Count} upcoming games from IGDB.", WorkerName, games.Count);

            await manager.UpsertAsync(games, ct);
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
