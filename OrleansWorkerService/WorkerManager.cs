using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;

public class WorkerManager : BackgroundService
{
    private readonly IClusterClient _client;
    private readonly ILogger<WorkerManager> _logger;

    public WorkerManager(IClusterClient client, ILogger<WorkerManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerManager is starting.");

        // Orleans �� WorkerGrain ���擾
        var workerGrain = _client.GetGrain<IWorkerGrain>("Worker1");

        _logger.LogInformation("Starting WorkerGrain...");
        await workerGrain.StartWork(); // Orleans ���Ǘ����邽�� 1 �񂾂��Ăׂ�OK

        _logger.LogInformation("WorkerManager has started WorkerGrain.");

        // �O������̒�~�v�����`�F�b�N���Ȃ��珈��
        while (!stoppingToken.IsCancellationRequested)
        {
            //_logger.LogInformation("WorkerManager is running. Monitoring WorkerGrain...");

            // Orleans��Grain�͎����Ǘ�����邽�߁A�ǉ��̏����Ȃ�
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("WorkerManager is stopping.");
    }

}
