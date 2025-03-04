using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;
using Timers;

public class PingWorker : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PingWorker> _logger;

    public PingWorker(IGrainFactory grainFactory, ILogger<PingWorker> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PingWorker started.");

        var pingGrain = _grainFactory.GetGrain<IPingGrain>("PingGrain");
        await pingGrain.Ping();

        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    _logger.LogInformation("Calling PingGrain.Ping()...");
        //    await pingGrain.Ping();
        //    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // 30分ごとに Ping() を呼ぶ
        //}

        _logger.LogInformation("PingWorker stopping.");
    }
}
