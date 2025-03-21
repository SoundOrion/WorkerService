﻿## **🔍 `PingGrain` の解説**
このコードは Orleans の `Grain`（`PingGrain`）を作成し、**`GrainTimer` と `Reminder` を使って定期的な処理を実行する** ものです。

### **📌 `PingGrain` の特徴**
1. **`GrainTimer` を使用して、3秒後に開始し、10秒ごとに処理を実行する**
2. **`Reminder` を使用して、1時間ごとに処理を実行する**
3. **Grain のライフサイクル管理を Orleans に任せながら、適切にタイマーやリマインダーを登録**
4. **`IDisposable` を実装し、リマインダーをクリーンアップ**

---

## **🛠 コードの詳細解説**
### **① `GrainTimer` の登録**
```csharp
// Timer を登録し、3秒後に開始し、10秒ごとに実行
timerRegistry.RegisterGrainTimer(
    grainContext,
    callback: static async (state, cancellationToken) =>
    {
        // ここに実行する処理を記述（省略）
        await Task.CompletedTask;
    },
    state: this,
    options: new GrainTimerCreationOptions
    {
        DueTime = TimeSpan.FromSeconds(3),  // 3秒後に開始
        Period = TimeSpan.FromSeconds(10)   // 10秒ごとに実行
    });
```
✅ **Orleans の `GrainTimer` を使って、一定間隔で処理を実行**  
✅ **この `Timer` は `Grain` がアクティブな間だけ動作し、非アクティブ化されると削除される**

---

### **② `Reminder` の登録**
```csharp
public async Task Ping()
{
    _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
        callingGrainId: GrainContext.GrainId,
        reminderName: ReminderName,
        dueTime: TimeSpan.Zero,           // 即時実行
        period: TimeSpan.FromHours(1));   // 1時間ごとに実行
}
```
✅ **`Reminder` を使って、1時間ごとに `Ping()` を実行する**  
✅ **`Reminder` は `Grain` が非アクティブ化されても定期的に実行される**

---

### **③ `Reminder` のクリーンアップ**
```csharp
void IDisposable.Dispose()
{
    if (_reminder is not null)
    {
        _reminderRegistry.UnregisterReminder(
            GrainContext.GrainId, _reminder);
    }
}
```
✅ **Grain の `Dispose()` が呼ばれた際に `Reminder` を解除する**  
✅ **不要な `Reminder` をクリーンアップしてメモリを節約**

---

## **📌 `BackgroundService` で `PingGrain` を定期的に実行**
Orleans の `Grain` は **外部からアクセスしないとアクティブ化されない** ため、  
**バックグラウンドサービス (`BackgroundService`) で定期的に `PingGrain.Ping()` を呼び出し、定期的に実行する仕組み** を作る。

---

### **✅ `BackgroundService` の実装**
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;

public class PingWorker : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PingWorker> _logger;

    public PingWorker(IGrainFactory grainFactory, ILogger<PingWorker> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PingWorker started.");

        var pingGrain = _grainFactory.GetGrain<IPingGrain>("PingGrain");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Calling PingGrain.Ping()...");
            await pingGrain.Ping();
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // 30分ごとに Ping() を呼ぶ
        }

        _logger.LogInformation("PingWorker stopping.");
    }
}
```
✅ **30分ごとに `PingGrain.Ping()` を実行**  
✅ **Orleans の `Grain` を定期的にアクティブ化し、定期処理を継続**  
✅ **キャンセルリクエスト (`stoppingToken`) を受け取ると停止する**

---

## **📌 `Program.cs`（Silo の設定）**
次に、Silo（Orleans サーバー）を起動し、`BackgroundService` を組み込む。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Reminders;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.AddConsole())
            .UseOrleans((context, siloBuilder) =>
            {
                // Orleans のクラスタリングをローカル環境で設定
                siloBuilder.UseLocalhostClustering();

                // Orleans の In-Memory Reminder を有効化
                siloBuilder.UseInMemoryReminderService();

                // メモリストレージを有効化（通常の Grains 用）
                siloBuilder.AddMemoryGrainStorage("default");

                // Reminder 用のストレージを追加
                siloBuilder.AddMemoryGrainStorage("reminder");

                // クラスタ設定
                siloBuilder.Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "WorkerServiceApp";
                });
            })
            .ConfigureServices((context, services) =>
            {
                // PingWorker をバックグラウンドサービスとして追加
                services.AddHostedService<PingWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}
```
✅ **Orleans の Silo を設定し、Reminder を有効化**  
✅ **`PingWorker` をバックグラウンドサービスとして登録**  
✅ **`BackgroundService` が `PingGrain` を定期的に呼び出す**

---

## **🚀 これで Orleans の `Grain` を `BackgroundService` で定期実行できる！**
### **🔍 変更点まとめ**
✅ **Orleans の `GrainTimer` を使って、一定間隔で処理を実行**  
✅ **`Reminder` を使って `Grain` が非アクティブ化されても定期実行を継続**  
✅ **`BackgroundService` で `Grain` を定期的にアクティブ化し、確実に処理を実行**  
✅ **Silo の設定に `UseInMemoryReminderService()` を追加し、Reminder を有効化**  

---

## **💡 これで Orleans の `Grain` を常駐プロセスのように使える！**
👉 **Orleans の `GrainTimer` + `Reminder` + `BackgroundService` を組み合わせることで、クラッシュに強い無限ループを実現できる！** 🚀









## **常に落ちないことが重要な場合、どの設計が最適か？**
**→ Orleans の `Reminder` をメインにして、`GrainTimer` は使わないのがベスト！** 🚀  
**理由:** Orleans の `Reminder` は **クラスタのノード（Silo）がクラッシュしても復旧可能で、処理が途切れないため**。

---

## **🔍 どの方法が適しているか？**
| 設計 | Orleans クラスタ再起動時 | Silo（ノード）クラッシュ時 | Grain が非アクティブ化されたとき | 負荷 |
|------|------------------|-------------------|---------------------|------|
| **GrainTimer (`KeepAlive = true`)** | ❌ 消える | ❌ 消える | ✅ 維持される | ✅ 軽い |
| **Reminder + GrainTimer（ハイブリッド）** | ✅ 復元される | ✅ Reminder により復旧 | ✅ Reminder により再アクティブ化 | ⚠ 若干負荷増 |
| **Reminder のみ（推奨）** | ✅ 復元される | ✅ Reminder により復旧 | ✅ Reminder により再アクティブ化 | ✅ 軽い |

💡 **「常に落ちない」ことを最重要視するなら、`Reminder` をメインにすべき！**  
💡 `GrainTimer` は **Silo がクラッシュすると消える** ので、安定性が必要なら `Reminder` のみの設計がよい。

---

## **✅ Orleans の `Reminder` のみを使った実装**
### **📌 1. `IHeartbeatGrain` インターフェース**
```csharp
using Orleans;
using Orleans.Runtime;
using System.Threading.Tasks;

namespace HeartbeatSystem
{
    public interface IHeartbeatGrain : IGrainWithStringKey, IRemindable
    {
        Task StartHeartbeat();
        Task ReceiveHeartbeat();
    }
}
```
✔ **`IRemindable` を継承することで `ReceiveReminder()` を実装可能にする**

---

### **📌 2. `HeartbeatGrain`（Reminder のみ）**
```csharp
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HeartbeatSystem
{
    public class HeartbeatGrain : Grain, IHeartbeatGrain, IRemindable
    {
        private readonly ILogger<HeartbeatGrain> _logger;
        private readonly IReminderRegistry _reminderRegistry;
        private IGrainReminder? _reminder;
        private const string ReminderName = "HeartbeatReminder";

        public HeartbeatGrain(IReminderRegistry reminderRegistry, ILogger<HeartbeatGrain> logger)
        {
            _reminderRegistry = reminderRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Grain がアクティブ化されたときに Reminder を登録
        /// </summary>
        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HeartbeatGrain activated.");

            _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
                GrainContext.GrainId,
                ReminderName,
                dueTime: TimeSpan.Zero,  // すぐに発火
                period: TimeSpan.FromMinutes(1));  // 1分ごとに発火

            await base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Orleans の Reminder によって定期的に呼ばれる
        /// </summary>
        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            if (reminderName == ReminderName)
            {
                _logger.LogInformation($"Heartbeat Reminder triggered at {DateTime.UtcNow}");

                await ReceiveHeartbeat();
            }
        }

        /// <summary>
        /// ハートビート処理
        /// </summary>
        public async Task ReceiveHeartbeat()
        {
            _logger.LogInformation($"[Heartbeat] Sent at {DateTime.UtcNow}");

            // ここで監視対象のシステムにハートビート送信処理を追加
            // 例: APIコール, DB更新 など

            await Task.CompletedTask;
        }

        /// <summary>
        /// Grain の非アクティブ化時に Reminder を維持
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing HeartbeatGrain.");

            if (_reminder is not null)
            {
                await _reminderRegistry.UnregisterReminder(GrainContext.GrainId, _reminder);
                _reminder = null;
            }
        }
    }
}
```

---

## **💡 Orleans の Reminder のみを使う理由**
1. **Orleans クラスタが再起動しても、Reminder により Grain が再アクティブ化される**
2. **GrainTimer を使わないため、Orleans のリソース消費が少なくなる**
3. **Reminder は Orleans の分散ストレージに保存されるため、Silo（ノード）がクラッシュしても動作する**
4. **非アクティブになっても、Reminder によって再アクティブ化される**
5. **長期間安定して動作させる用途に最適（ハートビートやバッチ処理向け）**

---

## **🎯 まとめ**
| 方法 | Orleans クラスタ再起動時 | Silo（ノード）クラッシュ時 | 負荷 | 推奨用途 |
|------|------------------|-------------------|------|---------|
| **GrainTimer (`KeepAlive = true`)** | ❌ 消える | ❌ 消える | ✅ 軽い | 短期間のリアルタイム処理 |
| **Reminder + GrainTimer（ハイブリッド）** | ✅ 復元される | ✅ Reminder により復旧 | ⚠ 若干負荷増 | 両方のメリットを活かしたい場合 |
| **Reminder のみ（推奨）** | ✅ 復元される | ✅ Reminder により復旧 | ✅ 軽い | **長期間の安定稼働** |

**☑ `Reminder` のみを使うことで、Silo のクラッシュや Orleans クラスタの再起動にも耐えられる！** 🚀