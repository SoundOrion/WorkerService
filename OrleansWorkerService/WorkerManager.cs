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

        // Orleans の WorkerGrain を取得
        var workerGrain = _client.GetGrain<IWorkerGrain>("Worker1");

        _logger.LogInformation("Starting WorkerGrain...");
        await workerGrain.StartWork(); // Orleans が管理するため 1 回だけ呼べばOK

        _logger.LogInformation("WorkerManager has started WorkerGrain.");

        // 外部からの停止要求をチェックしながら処理
        while (!stoppingToken.IsCancellationRequested)
        {
            //_logger.LogInformation("WorkerManager is running. Monitoring WorkerGrain...");

            // OrleansのGrainは自動管理されるため、追加の処理なし
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("WorkerManager is stopping.");
    }

}
