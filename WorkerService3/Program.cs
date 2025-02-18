using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService3;

var builder = WebApplication.CreateBuilder(args);

// Garnet を WebAPI サーバー内でセルフホスト
builder.Services.AddHostedService<GarnetHostService>();

// WorkerService を登録
builder.Services.AddHostedService<WorkerService>();

// Web API の設定
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
