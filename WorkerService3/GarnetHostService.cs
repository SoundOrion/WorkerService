using Garnet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorkerService3;

public class GarnetHostService : IHostedService
{
    private readonly ILogger<GarnetHostService> _logger;
    private GarnetServer? _garnetServer;
    private readonly string[] _garnetArgs = new[] { "--storage.memoryOnly true" };

    public GarnetHostService(ILogger<GarnetHostService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Garnet Server...");

        try
        {
            _garnetServer = new GarnetServer(_garnetArgs);
            _garnetServer.Start();
            _logger.LogInformation("Garnet Server started.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to start Garnet Server: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Garnet Server...");
        _garnetServer?.Dispose();
        return Task.CompletedTask;
    }
}

