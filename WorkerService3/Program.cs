using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

//// SQL Server ‚ÌÚ‘±î•ñ‚ğİ’è
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// WorkerService ‚ğ“o˜^
builder.Services.AddHostedService<WorkerService>();

// Web API ‚Ìİ’è
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
