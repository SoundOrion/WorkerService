using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Garnet.client;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly string _connectionString;
    private readonly GarnetClient _garnetClient;
    private bool _isRunning = false;

    public WorkerService(ILogger<WorkerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection");

        // Garnet クライアントを初期化（デフォルトの localhost:3278）
        _garnetClient = new GarnetClient("127.0.0.1", 3278);

        // 初回の状態を Garnet キャッシュにセット
        InitializeWorkerStatus();
    }

    private void InitializeWorkerStatus()
    {
        bool initialStatus = GetWorkerStatus();
        _garnetClient.StringSet("WorkerStatus", initialStatus ? "1" : "0");
        _isRunning = initialStatus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _isRunning = GetWorkerStatus(); // 最新の状態を取得し、直接 _isRunning に代入

            if (_isRunning)
            {
                _logger.LogInformation("WorkerService is running at: {time}", DateTimeOffset.Now);
            }
            else
            {
                _logger.LogInformation("WorkerService is paused due to cache flag.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("WorkerService is stopping.");
    }

    private bool GetWorkerStatus()
    {
        // Garnet からキャッシュを取得
        string? cachedStatus = _garnetClient.Get("WorkerStatus");

        if (cachedStatus != null)
        {
            return cachedStatus == "1";
        }

        // キャッシュがない場合は DB から取得
        bool dbStatus = GetWorkerStatusFromDatabase();

        // Garnet にキャッシュを保存（60秒有効）
        _garnetClient.StringSet("WorkerStatus", dbStatus ? "1" : "0", 60);

        return dbStatus;
    }

    private bool GetWorkerStatusFromDatabase()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqlCommand("SELECT TOP 1 EnableWorker FROM WorkerSettings", connection))
            {
                var result = command.ExecuteScalar();
                return result != null && (bool)result;
            }
        }
    }
}
