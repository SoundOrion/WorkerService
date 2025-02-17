using Microsoft.AspNetCore.Mvc;
using Garnet.client;

[Route("api/worker")]
[ApiController]
public class WorkerController : ControllerBase
{
    private readonly string _connectionString;
    private readonly GarnetClient _garnetClient;

    public WorkerController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _garnetClient = new GarnetClient("127.0.0.1", 3278);
    }

    [HttpGet("status")]
    public IActionResult GetWorkerStatus()
    {
        // Garnet からキャッシュを取得
        string? cachedStatus = _garnetClient.Get("WorkerStatus");

        if (cachedStatus != null)
        {
            return Ok(new { WorkerEnabled = cachedStatus == "1" });
        }

        // キャッシュがなければ DB から取得
        bool dbStatus = GetWorkerStatusFromDatabase();

        // Garnet にキャッシュを保存（60秒有効）
        _garnetClient.Set("WorkerStatus", dbStatus ? "1" : "0", 60);

        return Ok(new { WorkerEnabled = dbStatus });
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

        // Garnetキャッシュを更新
        _garnetClient.StringSet("WorkerStatus", enableWorker ? "1" : "0", 60);

        return Ok("WorkerSettings updated.");
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
