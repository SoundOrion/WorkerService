using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorkerService;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly IOptionsMonitor<WorkerSettings> _settings;

    public WorkerService(ILogger<WorkerService> logger, IOptionsMonitor<WorkerSettings> settings)
    {
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // フラグを監視し、オンの時だけ動作
            if (_settings.CurrentValue.EnableWorker)
            {
                _logger.LogInformation("WorkerService is running at: {time}", DateTimeOffset.Now);
            }
            else
            {
                _logger.LogInformation("WorkerService is paused due to config flag.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // 3秒ごとに実行
        }

        _logger.LogInformation("WorkerService is stopping.");
    }
}
