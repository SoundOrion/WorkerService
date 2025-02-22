using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;

public class WorkerManager : BackgroundService
{
    private readonly IClusterClient _client;
    private readonly ILogger<WorkerManager> _logger;

    public WorkerManager(IClusterClient client, ILogger<WorkerManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerManager is starting.");

        var workerGrain = _client.GetGrain<IWorkerGrain>("Worker1");
        await workerGrain.StartWork(stoppingToken);

        _logger.LogInformation("WorkerManager is stopping.");
    }
}
