using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MyBackgroundService : BackgroundService
{
    private readonly ILogger<MyBackgroundService> _logger;

    public MyBackgroundService(ILogger<MyBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting external process...");
                await RunExternalProcessAsync(@"C:\path\to\yourapp.exe", "", stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing process.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);  // 1分ごとに実行
        }
    }

    private async Task RunExternalProcessAsync(string filePath, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, args) => _logger.LogInformation($"Output: {args.Data}");
            process.ErrorDataReceived += (sender, args) => _logger.LogError($"Error: {args.Data}");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
        }
    }
}
