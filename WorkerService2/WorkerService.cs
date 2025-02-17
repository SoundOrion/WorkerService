using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using WorkerService2;

namespace WorkerService2;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly IOptionsMonitor<WorkerSettings> _settings;
    private bool _isRunning = false; // 手動で制御するためのフラグ

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
            if (_isRunning && _settings.CurrentValue.EnableWorker)
            {
                _logger.LogInformation("WorkerService is running at: {time}", DateTimeOffset.Now);
            }
            else
            {
                _logger.LogInformation("WorkerService is paused.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("WorkerService is stopping.");
    }

    // WebAPI から制御するためのメソッド
    public void StartWorker()
    {
        _isRunning = true;
        _logger.LogInformation("WorkerService has been started manually.");
    }

    public void StopWorker()
    {
        _isRunning = false;
        _logger.LogInformation("WorkerService has been stopped manually.");
    }
}
