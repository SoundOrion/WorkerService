using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Timers
{
    public interface IPingGrain : IGrainWithStringKey
    {
        Task Ping();
    }

    public class PingGrain : Grain, IPingGrain, IDisposable
    {
        private const string ReminderName = "ExampleReminder";
        private readonly IReminderRegistry _reminderRegistry;
        private readonly ILogger<PingGrain> _logger;
        private readonly ITimerRegistry _timerRegistry;
        private IGrainReminder? _reminder;
        private IGrainTimer? _timer;

        public IGrainContext GrainContext { get; }

        public PingGrain(
            ITimerRegistry timerRegistry,
            IReminderRegistry reminderRegistry,
            IGrainContext grainContext,
            ILogger<PingGrain> logger)
        {
            _timerRegistry = timerRegistry;
            _reminderRegistry = reminderRegistry;
            GrainContext = grainContext;
            _logger = logger;
        }

        /// <summary>
        /// Grain のアクティブ化時に実行（Reminder の登録 & GrainTimer の開始）
        /// </summary>
        public async Task Ping()
        {
            _logger.LogInformation("Ping() called: Registering Reminder...");

            _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
                callingGrainId: GrainContext.GrainId,
                reminderName: ReminderName,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromHours(1)); // 1時間ごとに発火
        }

        /// <summary>
        /// Orleans の Reminder によって定期的に呼ばれる
        /// </summary>
        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            if (reminderName == ReminderName)
            {
                _logger.LogInformation("Reminder triggered! Reactivating Grain and starting Timer...");
                StartTimer();
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// Grain のアクティブ化時に呼ばれる
        /// </summary>
        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PingGrain activated. Starting Timer...");

            StartTimer();

            return base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// GrainTimer を登録し、3秒後に開始し、10秒ごとに実行
        /// </summary>
        private void StartTimer()
        {
            _logger.LogInformation("Registering GrainTimer...");

            _timer = _timerRegistry.RegisterGrainTimer(
                GrainContext,
                callback: async (state, cancellationToken) =>
                {
                    _logger.LogInformation($"GrainTimer executed at {DateTime.Now}");
                    // ここで定期的に行いたい処理を記述
                    await DoWorkAsync();
                },
                state: this,
                options: new GrainTimerCreationOptions
                {
                    DueTime = TimeSpan.FromSeconds(3),  // 3秒後に開始
                    Period = TimeSpan.FromSeconds(10)   // 10秒ごとに実行
                });
        }

        /// <summary>
        /// 10秒ごとに実行される処理
        /// </summary>
        private async Task DoWorkAsync()
        {
            _logger.LogInformation($"[DoWork] Processing data at {DateTime.Now}");

            // ここで何らかの処理を実行（例：データベース更新、API呼び出しなど）
            await Task.Delay(500); // ダミー処理（0.5秒の遅延）

            _logger.LogInformation($"[DoWork] Processing completed.");
        }

        /// <summary>
        /// Grain の非アクティブ化時に呼ばれる
        /// </summary>
        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"PingGrain deactivating. Reason: {reason.ReasonCode}");
            _timer?.Dispose();  // Timer を停止
            return Task.CompletedTask;
        }

        /// <summary>
        /// Grain の破棄時に Reminder を解除
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_reminder is not null)
            {
                _reminderRegistry.UnregisterReminder(GrainContext.GrainId, _reminder);
            }
        }
    }
}
