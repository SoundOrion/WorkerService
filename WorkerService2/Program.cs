using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService2;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json の設定を WorkerSettings クラスにバインド
builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("WorkerSettings"));

// HostedService（バックグラウンドサービス）をシングルトンとして登録
builder.Services.AddSingleton<WorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkerService>());

// Web API 用のコントローラーを追加
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers(); // API ルートを有効化
app.MapGet("/", () => "Web API is running");

app.Run();
