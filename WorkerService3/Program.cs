using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService3;

var builder = WebApplication.CreateBuilder(args);

// Garnet �� WebAPI �T�[�o�[���ŃZ���t�z�X�g
builder.Services.AddHostedService<GarnetHostService>();

// WorkerService ��o�^
builder.Services.AddHostedService<WorkerService>();

// Web API �̐ݒ�
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
