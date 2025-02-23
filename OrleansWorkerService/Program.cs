using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.AddConsole()) // ここで設定
            .UseOrleans((context, siloBuilder) =>
            {
                siloBuilder.UseLocalhostClustering();
                siloBuilder.AddMemoryGrainStorage("urls");
                siloBuilder.Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "WorkerServiceApp";
                });
                // ✅ 環境が Development のときだけ Orleans Dashboard を有効化
                if (context.HostingEnvironment.IsDevelopment())
                {
                    siloBuilder.UseDashboard(options =>
                    {
                        options.Port = 8080;
                        options.HostSelf = true;
                    });
                }
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<WorkerManager>();
            })
            .Build();

        await host.RunAsync();
    }
}
