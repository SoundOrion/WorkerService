using HeartbeatSystem;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;
using Timers;

public class HeartbeatWorker : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HeartbeatWorker> _logger;

    public HeartbeatWorker(IGrainFactory grainFactory, ILogger<HeartbeatWorker> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatWorker started.");

        var HeartbeatGrain = _grainFactory.GetGrain<IHeartbeatGrain>("HeartbeatGrain");
        await HeartbeatGrain.StartHeartbeat();

        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    _logger.LogInformation("Calling PingGrain.Ping()...");
        //    await pingGrain.Ping();
        //    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // 30分ごとに Ping() を呼ぶ
        //}

        //_logger.LogInformation("HeartbeatWorker stopping.");
    }
}
