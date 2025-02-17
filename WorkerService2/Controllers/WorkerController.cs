using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace WorkerService2.Controllers;

[Route("api/worker")]
[ApiController]
public class WorkerController : ControllerBase
{
    private readonly WorkerService _workerService;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<WorkerSettings> _settings;

    public WorkerController(WorkerService workerService, IConfiguration configuration, IOptionsMonitor<WorkerSettings> settings)
    {
        _workerService = workerService;
        _configuration = configuration;
        _settings = settings;
    }

    [HttpPost("start")]
    public IActionResult StartWorker()
    {
        _workerService.StartWorker();
        return Ok("WorkerService has been started.");
    }

    [HttpPost("stop")]
    public IActionResult StopWorker()
    {
        _workerService.StopWorker();
        return Ok("WorkerService has been stopped.");
    }

    [HttpGet("status")]
    public IActionResult GetWorkerStatus()
    {
        bool isEnabled = _settings.CurrentValue.EnableWorker;
        return Ok(new { WorkerEnabled = isEnabled });
    }

    [HttpPost("update-config")]
    public IActionResult UpdateConfig([FromBody] WorkerSettings newSettings)
    {
        try
        {
            var configPath = "appsettings.json";

            // JSON を読み込み
            string json = System.IO.File.ReadAllText(configPath);
            using JsonDocument document = JsonDocument.Parse(json);
            var jsonObj = document.RootElement.Clone();

            // `EnableWorker` の値を更新
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var property in jsonObj.EnumerateObject())
                {
                    if (property.NameEquals("WorkerSettings"))
                    {
                        writer.WritePropertyName(property.Name);
                        writer.WriteStartObject();
                        foreach (var subProperty in property.Value.EnumerateObject())
                        {
                            if (subProperty.NameEquals("EnableWorker"))
                            {
                                writer.WriteString(subProperty.Name, newSettings.EnableWorker.ToString().ToLower());
                            }
                            else
                            {
                                subProperty.WriteTo(writer);
                            }
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }

            // ファイルに書き込み
            System.IO.File.WriteAllText(configPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));

            return Ok("WorkerSettings has been updated.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating config: {ex.Message}");
        }
    }
}
