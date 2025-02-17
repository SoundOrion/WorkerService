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

        // Garnet �N���C�A���g���������i�f�t�H���g�� localhost:3278�j
        _garnetClient = new GarnetClient("127.0.0.1", 3278);

        // ����̏�Ԃ� Garnet �L���b�V���ɃZ�b�g
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
            _isRunning = GetWorkerStatus(); // �ŐV�̏�Ԃ��擾���A���� _isRunning �ɑ��

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
        // Garnet ����L���b�V�����擾
        string? cachedStatus = _garnetClient.Get("WorkerStatus");

        if (cachedStatus != null)
        {
            return cachedStatus == "1";
        }

        // �L���b�V�����Ȃ��ꍇ�� DB ����擾
        bool dbStatus = GetWorkerStatusFromDatabase();

        // Garnet �ɃL���b�V����ۑ��i60�b�L���j
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
