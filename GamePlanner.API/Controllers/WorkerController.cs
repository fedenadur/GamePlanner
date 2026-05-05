using GamePlanner.API.Managers;
using GamePlanner.API.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WorkerController(
    WorkerStatusManager statusManager,
    IServiceScopeFactory scopeFactory) : ControllerBase
{
    private static readonly HashSet<string> KnownWorkers =
    [
        GameSyncWorker.WorkerName,
        GameImageSyncWorker.WorkerName
    ];

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await statusManager.GetAllAsync(ct));

    [HttpPost("{name}/run")]
    public IActionResult Run(string name)
    {
        if (!KnownWorkers.Contains(name))
            return NotFound($"Worker '{name}' not found.");

        // Fire-and-forget — do NOT await
        _ = Task.Run(() => RunWorkerAsync(name));

        return Accepted(new { message = $"{name} triggered." });
    }

    private async Task RunWorkerAsync(string name)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        if (name == GameSyncWorker.WorkerName)
        {
            var worker = new GameSyncWorker(scopeFactory,
                scope.ServiceProvider.GetRequiredService<ILogger<GameSyncWorker>>());
            await worker.RunAsync(CancellationToken.None);
        }
        else if (name == GameImageSyncWorker.WorkerName)
        {
            var worker = new GameImageSyncWorker(scopeFactory,
                scope.ServiceProvider.GetRequiredService<ILogger<GameImageSyncWorker>>());
            await worker.RunAsync(CancellationToken.None);
        }
    }
}
