using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

//// SQL Server �̐ڑ�����ݒ�
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// WorkerService ��o�^
builder.Services.AddHostedService<WorkerService>();

// Web API �̐ݒ�
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
