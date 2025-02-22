using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

public interface IWorkerGrain : IGrainWithStringKey
{
    Task StartWork(CancellationToken cancellationToken);
}

public class WorkerGrain : Grain, IWorkerGrain
{
    private readonly ILogger<WorkerGrain> _logger;
    private CancellationTokenSource _cts;

    public WorkerGrain(ILogger<WorkerGrain> logger)
    {
        _logger = logger;
    }

    public async Task StartWork(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WorkerGrain is starting.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        while (!_cts.Token.IsCancellationRequested)
        {
            _logger.LogInformation("WorkerGrain is running.");
            await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token); // ✅ CancellationToken を適用
        }

        _logger.LogInformation("WorkerGrain is stopping.");
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"WorkerGrain deactivating. Reason: {reason.ReasonCode}");
        _cts?.Cancel(); // ✅ Grainが削除されるときにキャンセルを発行
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}


//public interface IWorkerGrain : IGrainWithStringKey
//{
//    Task StartWork();
//}

//public class WorkerGrain : Grain, IWorkerGrain
//{
//    private readonly ILogger<WorkerGrain> _logger;
//    private IGrainTimer _timer;

//    public WorkerGrain(ILogger<WorkerGrain> logger)
//    {
//        _logger = logger;
//    }

//    public override Task OnActivateAsync(CancellationToken cancellationToken)
//    {
//        _logger.LogInformation("WorkerGrain activated.");

//        // ✅ 正しい形式で RegisterGrainTimer を呼び出す
//        _timer = this.RegisterGrainTimer(DoWork, TimeSpan.Zero, TimeSpan.FromSeconds(3));

//        return base.OnActivateAsync(cancellationToken);
//    }

//    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
//    {
//        _logger.LogInformation($"WorkerGrain deactivating. Reason: {reason.ReasonCode}");
//        _timer?.Dispose();
//        return base.OnDeactivateAsync(reason, cancellationToken);
//    }

//    public Task StartWork()
//    {
//        _logger.LogInformation("WorkerGrain StartWork() called.");
//        return Task.CompletedTask;
//    }

//    private Task DoWork() // ✅ Func<Task> の形式に修正
//    {
//        _logger.LogInformation("WorkerGrain is running.");
//        return Task.CompletedTask;
//    }
//}
