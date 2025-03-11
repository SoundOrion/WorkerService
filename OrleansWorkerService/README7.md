## **NATS Consumer を Orleans で自動回復・耐障害性の高い設計にする**
  
NATS の **Consumer を常駐アプリケーションとして動作** させ、**人の手を介さずに自動で回復する耐障害性のあるアーキテクチャ** を Orleans を使って実装します。

---

### **🔥 耐障害性を高めるポイント**
| 対策 | Orleans の活用 |
|------|--------------|
| **Consumer の自動リスタート** | Orleans の **Grain 再起動** を利用 |
| **複数ノードでの負荷分散** | Orleans **クラスタリング** を活用 |
| **メッセージの再処理** | NATS **JetStream Durable Consumer** |
| **障害時の自己修復** | Orleans **リマインダー (Reminder)** を使用 |

---

## **🛠 設計概要**
1. **NATS Durable Consumer を Orleans の WorkerGrain として実装**
2. **Consumer が落ちた場合、Orleans が自動的に再起動**
3. **NATS JetStream Durable Consumer を利用し、メッセージをロストしない**
4. **Orleans Reminder を活用し、定期的に Consumer の状態を監視**
5. **複数の Orleans ノードを活用し、スケールアウト可能**

---

## **📌 Orleans の設計**
### **🔹 主要な Orleans Grain**
| Orleans Grain | 役割 |
|--------------|------|
| **`IQueueGrain`** | NATS からメッセージを取得・処理 |
| **`IWorkerGrain`** | 実際の NATS Consumer (常駐) |
| **`IMonitorGrain`** | Consumer の監視・自己修復 |

---

## **📜 Orleans の実装**
### **1️⃣ Orleans の Consumer 実装**
#### **`IWorkerGrain.cs` (Consumer のインターフェース)**
```csharp
using Orleans;
using System.Threading.Tasks;

public interface IWorkerGrain : IGrainWithStringKey
{
    Task StartConsuming();
    Task StopConsuming();
}
```

---

#### **`WorkerGrain.cs` (耐障害性 NATS Consumer)**
```csharp
using Orleans;
using NATS.Client;
using NATS.Client.JetStream;
using System;
using System.Text.Json;
using System.Threading.Tasks;

public class WorkerGrain : Grain, IWorkerGrain
{
    private IConnection _natsConnection;
    private IJetStream _jetStream;
    private IJetStreamPushSyncSubscription _subscription;

    public override async Task OnActivateAsync()
    {
        var cf = new ConnectionFactory();
        _natsConnection = cf.CreateConnection("nats://127.0.0.1:4222");
        _jetStream = _natsConnection.CreateJetStreamContext();
        Console.WriteLine($"[WorkerGrain] Connected to NATS JetStream.");

        await StartConsuming();
    }

    public async Task StartConsuming()
    {
        Console.WriteLine("[WorkerGrain] Starting NATS Consumer...");
        var consumerConfig = new ConsumerConfig
        {
            DurableName = "durable_worker",
            AckPolicy = ConsumerConfigAckPolicy.Explicit
        };

        var consumer = await _jetStream.CreateOrUpdateConsumerAsync("ORDERS", consumerConfig);
        _subscription = consumer.SubscribePushSync<Order>();

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var msg = _subscription.NextMessage();
                    if (msg != null)
                    {
                        var order = JsonSerializer.Deserialize<Order>(msg.Data);
                        Console.WriteLine($"[WorkerGrain] Processing Order {order.Id}: {order.Description}");
                        await msg.AckAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WorkerGrain] Consumer Error: {ex.Message}");
                }

                await Task.Delay(500); // CPU 負荷を下げる
            }
        });
    }

    public async Task StopConsuming()
    {
        Console.WriteLine("[WorkerGrain] Stopping NATS Consumer...");
        _subscription?.Dispose();
    }

    public override async Task OnDeactivateAsync()
    {
        await StopConsuming();
        _natsConnection?.Dispose();
    }
}
```

---

### **2️⃣ Orleans の Consumer 監視機構**
#### **`IMonitorGrain.cs` (Consumer 監視インターフェース)**
```csharp
using Orleans;
using System.Threading.Tasks;

public interface IMonitorGrain : IGrainWithStringKey
{
    Task MonitorWorkers();
}
```

---

#### **`MonitorGrain.cs` (Consumer の監視・自動修復)**
```csharp
using Orleans;
using System;
using System.Threading.Tasks;

public class MonitorGrain : Grain, IMonitorGrain, IRemindable
{
    public override async Task OnActivateAsync()
    {
        await RegisterOrUpdateReminder("WorkerMonitor", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == "WorkerMonitor")
        {
            await MonitorWorkers();
        }
    }

    public async Task MonitorWorkers()
    {
        Console.WriteLine("[MonitorGrain] Checking Worker Health...");

        var worker = GrainFactory.GetGrain<IWorkerGrain>("order_worker");
        await worker.StartConsuming();
    }
}
```

---

### **3️⃣ Orleans クラスタのエントリーポイント**
#### **`Program.cs`**
```csharp
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

var host = Host.CreateDefaultBuilder()
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering();
    })
    .Build();

await host.RunAsync();
```

---

## **🛠 Orleans × NATS の耐障害性ポイント**
| 課題 | Orleans での解決策 |
|------|-----------------|
| **Consumer がクラッシュする** | Orleans の **Grain 自動再起動** |
| **NATS メッセージをロストする** | NATS **JetStream Durable Consumer** |
| **Consumer の状態監視** | Orleans の **Reminder (定期チェック)** |
| **負荷が増えたときにスケールアウト** | Orleans **クラスタで WorkerGrain を水平スケール** |

---

## **💡 Orleans × NATS の耐障害性まとめ**
✅ **Orleans WorkerGrain を NATS Consumer にする**  
✅ **WorkerGrain のクラッシュ時、自動再起動**  
✅ **MonitorGrain による定期的な Consumer 状態監視**  
✅ **NATS JetStream Durable Consumer により、メッセージをロストしない**  
✅ **Orleans のクラスタ化により、自動で負荷分散**  

---

## **🚀 次のステップ**
1. **Kubernetes (K8s) で Orleans + NATS をデプロイ**
2. **Orleans の永続ストレージを活用（Azure Table, MongoDB, PostgreSQL）**
3. **メトリクス監視（Prometheus + Grafana）**
4. **負荷テストを実施してスケール検証**

---

### **🛠 Orleans × NATS でフルオートな耐障害システムを構築！**
この設計で、人手を介さずに **自己修復する分散メッセージングシステム** を Orleans + NATS で実現できます 🚀