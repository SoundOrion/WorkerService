using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HeartbeatSystem
{
    public interface IHeartbeatGrain : IGrainWithStringKey
    {
        Task StartHeartbeat();
        Task ReceiveHeartbeat();
        Task ReceiveReminder(string reminderName, TickStatus status);
    }

    public class HeartbeatGrain : Grain, IHeartbeatGrain, IDisposable
    {
        private readonly ILogger<HeartbeatGrain> _logger;
        private readonly ITimerRegistry _timerRegistry;
        private readonly IReminderRegistry _reminderRegistry;
        private IGrainTimer? _heartbeatTimer;
        private IGrainReminder? _reminder;
        private const string ReminderName = "HeartbeatReminder";

        public HeartbeatGrain(ITimerRegistry timerRegistry, IReminderRegistry reminderRegistry, ILogger<HeartbeatGrain> logger)
        {
            _timerRegistry = timerRegistry;
            _reminderRegistry = reminderRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Grain がアクティブ化されたときにハートビートを開始
        /// </summary>
        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HeartbeatGrain activated.");
            await StartHeartbeat();
            await base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// GrainTimer と Reminder を設定し、ハートビートを開始
        /// </summary>
        public async Task StartHeartbeat()
        {
            _logger.LogInformation("Starting Heartbeat...");

            // GrainTimer の登録（5秒ごとにハートビートを送信）
            _heartbeatTimer?.Dispose(); // 既存のタイマーがあれば破棄
            _heartbeatTimer = _timerRegistry.RegisterGrainTimer(
                GrainContext,
                async (state, cancellationToken) => { await ReceiveHeartbeat(); },
                state: this,
                options: new GrainTimerCreationOptions
                {
                    DueTime = TimeSpan.Zero,    // すぐに開始
                    Period = TimeSpan.FromSeconds(5)  // 5秒ごとに実行
                });

            // Reminder の登録（10分ごとに発火、Grain が非アクティブでもリマインダーがアクティブ化）
            if (_reminder == null)
            {
                _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
                GrainContext.GrainId,
                ReminderName,
                dueTime: TimeSpan.Zero,  // すぐに発火
                period: TimeSpan.FromMinutes(10));  // 10分ごとに発火（バックアップ用）
            }
        }

        /// <summary>
        /// 定期的にハートビートを送信する処理
        /// </summary>
        public async Task ReceiveHeartbeat()
        {
            _logger.LogInformation($"[Heartbeat] Sent at {DateTime.UtcNow}");

            // ここで監視対象のシステムにハートビート送信処理を追加
            // 例: APIコール, DB更新 など

            await Task.CompletedTask;
        }

        /// <summary>
        /// Orleans の Reminder によって定期的に呼ばれる
        /// </summary>
        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            if (reminderName == ReminderName)
            {
                _logger.LogInformation("Heartbeat Reminder triggered. Restarting timer.");
                await StartHeartbeat();
            }
        }

        /// <summary>
        /// Grain が非アクティブ化される前に実行される
        /// </summary>
        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _logger.LogWarning($"HeartbeatGrain deactivating. Reason: {reason.ReasonCode}");

            // Timer を破棄
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Grain が破棄されるときに Reminder を解除
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
