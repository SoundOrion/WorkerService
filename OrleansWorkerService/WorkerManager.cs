using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

public class WorkerManager : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<WorkerManager> _logger;

    public WorkerManager(IGrainFactory grainFactory, ILogger<WorkerManager> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerManager is starting.");

        // Orleans の WorkerGrain を取得
        var workerGrain = _grainFactory.GetGrain<IWorkerGrain>("Worker1");

        _logger.LogInformation("Starting WorkerGrain...");
        await workerGrain.StartWork();

        _logger.LogInformation("WorkerManager has started WorkerGrain.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // ✅ `IManagementGrain` から Orleans のメトリクスを取得
            var managementGrain = _grainFactory.GetGrain<IManagementGrain>(0);

            // **① アクティブな Grain の統計情報を取得**
            var grainStats = await managementGrain.GetSimpleGrainStatistics();
            foreach (var stat in grainStats)
            {
                _logger.LogInformation($"📊 GrainType: {stat.GrainType}, ActivationCount: {stat.ActivationCount}");
            }

            // **② Silo の状態を取得（Orleans 7 では `GetRuntimeStatistics()` は削除されているため `GetHosts()` を利用）**
            var hosts = await managementGrain.GetHosts();
            foreach (var host in hosts)
            {
                _logger.LogInformation($"🏠 Silo: {host.Key}, Status: {host.Value}");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("WorkerManager is stopping.");
    }
}

