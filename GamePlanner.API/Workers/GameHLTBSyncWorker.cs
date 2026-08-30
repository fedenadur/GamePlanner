using GamePlanner.API.Managers;
using GamePlanner.API.Services;

namespace GamePlanner.API.Workers;

public class GameHLTBSyncWorker(IServiceScopeFactory scopeFactory, ILogger<GameHLTBSyncWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "GameHLTBSyncWorker";
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
        var csvService          = scope.ServiceProvider.GetRequiredService<CsvGameService>();
        var hltbManager         = scope.ServiceProvider.GetRequiredService<GameHLTBManager>();
        var statusManager       = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

        try
        {
            await statusManager.SetRunningAsync(WorkerName, ct);

            var (records, skipped, failureDetail) = csvService.ReadGames();

            if (records.Count == 0)
            {
                logger.LogWarning("{Worker}: no records to process.", WorkerName);
                await statusManager.SetCompletedAsync(WorkerName, skipped, failureDetail);

                if (skipped > 0)
                    logger.LogWarning("{Worker}: {FailedCount} item(s) failed.", WorkerName, skipped);

                return;
            }

            await hltbManager.UpsertAsync(records, ct);
            await statusManager.SetCompletedAsync(WorkerName, skipped, failureDetail);

            if (skipped > 0)
                logger.LogWarning("{Worker}: {FailedCount} item(s) failed.", WorkerName, skipped);
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
