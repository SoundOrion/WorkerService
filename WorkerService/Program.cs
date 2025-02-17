using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WorkerService;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json の設定を WorkerSettings クラスにバインド
builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("WorkerSettings"));

// HostedService（バックグラウンドサービス）を登録
builder.Services.AddHostedService<WorkerService.WorkerService>();

// Web API のエンドポイントを作成
var app = builder.Build();

app.MapGet("/", () => "Web API is running");

app.Run();
