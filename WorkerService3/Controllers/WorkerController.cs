using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Data.SqlClient;

[Route("api/worker")]
[ApiController]
public class WorkerController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IDatabase _redisDatabase;

    public WorkerController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");

        var redis = ConnectionMultiplexer.Connect("127.0.0.1:3278");
        _redisDatabase = redis.GetDatabase();
    }

    [HttpGet("status")]
    public IActionResult GetWorkerStatus()
    {
        string cachedStatus = _redisDatabase.StringGet("WorkerStatus");

        if (!string.IsNullOrEmpty(cachedStatus))
        {
            return Ok(cachedStatus == "1");
        }

        bool dbStatus = GetWorkerStatusFromDatabase();
        _redisDatabase.StringSet("WorkerStatus", dbStatus ? "1" : "0", TimeSpan.FromSeconds(60));

        return Ok(dbStatus);
    }

    [HttpPost("update")]
    public IActionResult UpdateWorkerStatus([FromBody] bool enableWorker)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqlCommand("UPDATE WorkerSettings SET EnableWorker = @EnableWorker", connection))
            {
                command.Parameters.AddWithValue("@EnableWorker", enableWorker);
                command.ExecuteNonQuery();
            }
        }

        // Garnet キャッシュを更新
        _redisDatabase.StringSet("WorkerStatus", enableWorker ? "1" : "0", TimeSpan.FromSeconds(60));

        return Ok();
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
