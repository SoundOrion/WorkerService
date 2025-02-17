using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService2;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json �̐ݒ�� WorkerSettings �N���X�Ƀo�C���h
builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("WorkerSettings"));

// HostedService�i�o�b�N�O���E���h�T�[�r�X�j���V���O���g���Ƃ��ēo�^
builder.Services.AddSingleton<WorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkerService>());

// Web API �p�̃R���g���[���[��ǉ�
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers(); // API ���[�g��L����
app.MapGet("/", () => "Web API is running");

app.Run();
