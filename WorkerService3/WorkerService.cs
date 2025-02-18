using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly string _connectionString;
    private readonly IDatabase _redisDatabase;
    private bool _isRunning = false;

    public WorkerService(ILogger<WorkerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection");

        // StackExchange.Redis を使用して Garnet に接続
        var redis = ConnectionMultiplexer.Connect("127.0.0.1:3278");
        _redisDatabase = redis.GetDatabase();

        // 初回の状態を Garnet キャッシュにセット
        InitializeWorkerStatus();
    }

    private void InitializeWorkerStatus()
    {
        bool initialStatus = GetWorkerStatus();
        _redisDatabase.StringSet("WorkerStatus", initialStatus ? "1" : "0");
        _isRunning = initialStatus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _isRunning = GetWorkerStatus();

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
        // Garnet（Redis）からキャッシュを取得
        string cachedStatus = _redisDatabase.StringGet("WorkerStatus");

        if (!string.IsNullOrEmpty(cachedStatus))
        {
            return cachedStatus == "1";
        }

        // キャッシュがない場合は DB から取得
        bool dbStatus = GetWorkerStatusFromDatabase();

        // Garnet にキャッシュを保存（60秒有効）
        _redisDatabase.StringSet("WorkerStatus", dbStatus ? "1" : "0", TimeSpan.FromSeconds(60));

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
