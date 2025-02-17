using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

//// SQL Server の接続情報を設定
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// WorkerService を登録
builder.Services.AddHostedService<WorkerService>();

// Web API の設定
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
