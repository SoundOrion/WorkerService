using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WorkerService;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json �̐ݒ�� WorkerSettings �N���X�Ƀo�C���h
builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("WorkerSettings"));

// HostedService�i�o�b�N�O���E���h�T�[�r�X�j��o�^
builder.Services.AddHostedService<WorkerService.WorkerService>();

// Web API �̃G���h�|�C���g���쐬
var app = builder.Build();

app.MapGet("/", () => "Web API is running");

app.Run();
