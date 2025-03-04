using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

public interface IWorkerGrain : IGrainWithStringKey
{
    Task StartWork();
}

public class WorkerGrain : Grain, IWorkerGrain
{
    private readonly ILogger<WorkerGrain> _logger;
    private IGrainTimer _timer;

    public WorkerGrain(ILogger<WorkerGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WorkerGrain activated.");

        // ✅ 正しい形式で RegisterGrainTimer を呼び出す
        var options = new GrainTimerCreationOptions
        {
            DueTime = TimeSpan.Zero,
            Period = TimeSpan.FromSeconds(5),
            Interleave = false,    // 他のメソッドと並行実行
            KeepAlive = true  // Grain を非アクティブ化させない
        };

        _timer = this.RegisterGrainTimer(DoWork, options);

        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"WorkerGrain deactivating. Reason: {reason.ReasonCode}");
        _timer?.Dispose();
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task StartWork()
    {
        //_logger.LogInformation("WorkerGrain StartWork() called.");
        return Task.CompletedTask;
    }

    private Task DoWork() // ✅ Func<Task> の形式に修正
    {
        _logger.LogInformation($"WorkerGrain is running. {DateTime.Now}");
        return Task.CompletedTask;
    }

    private int _failureCount = 0;
    private async Task ExceptionWork() // ✅ Func<Task> の形式に修正
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            _logger.LogInformation("WorkerGrain is running.");

            throw new InvalidOperationException("Something went wrong!");
        }
        catch (Exception ex)
        {
            _failureCount++;
            _logger.LogError(ex, $"WorkerGrain encountered an error. Failure count: {_failureCount}");

            if (_failureCount >= 3)
            {
                _logger.LogError("Too many failures, shutting down Grain.");
                DeactivateOnIdle(); // Orleans にこの Grain を削除させる
            }
        }
    }
}
