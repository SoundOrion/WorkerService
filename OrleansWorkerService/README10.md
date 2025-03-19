### **Loki への転送効率を上げるために Bulk 送信を行う方法**

Loki には **バルク送信 (Bulk Sending)** の仕組みがあり、複数のログをまとめて送ることで転送効率を向上させられます。具体的には、Loki の `POST /loki/api/v1/push` API では **複数のログエントリを一括で送信** できます。

---

## **1. Bulk 送信の基本構造**
Loki に送るデータの JSON フォーマットは、**1回のリクエストで複数のログを含める** ことができます。

### **フォーマット**
```json
{
  "streams": [
    {
      "stream": { "job": "csharp-app", "level": "info" },
      "values": [
        ["1710842134567890000", "First log message"],
        ["1710842134567990000", "Second log message"]
      ]
    }
  ]
}
```
**ポイント**
- `values` の配列に **複数のログを追加** して 1 回の HTTP リクエストで送信可能
- これにより **リクエスト数を削減し、転送効率を向上**

---

## **2. C# で Bulk 送信を実装**
C# で **複数のログを一括送信** する実装を紹介します。

### **C# コード: Bulk ログ送信**
```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string lokiUrl = "http://localhost:3100/loki/api/v1/push";
    
    static async Task Main()
    {
        var logs = new List<(DateTimeOffset Timestamp, string Message)>
        {
            (DateTimeOffset.UtcNow, "First log message"),
            (DateTimeOffset.UtcNow.AddMilliseconds(10), "Second log message"),
            (DateTimeOffset.UtcNow.AddMilliseconds(20), "Third log message")
        };

        await SendLogsToLoki(logs);
    }

    static async Task SendLogsToLoki(List<(DateTimeOffset Timestamp, string Message)> logEntries)
    {
        var logValues = new List<string[]>();

        foreach (var log in logEntries)
        {
            string timestamp = log.Timestamp.ToUnixTimeNanoseconds().ToString();
            logValues.Add(new string[] { timestamp, log.Message });
        }

        var payload = new
        {
            streams = new[]
            {
                new
                {
                    stream = new { job = "csharp-app", level = "info" },
                    values = logValues
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(lokiUrl, content);
        Console.WriteLine($"Response: {response.StatusCode}");
    }
}
```

---

## **3. Bulk 送信のポイント**
- **リクエストのオーバーヘッドを削減**
  - 1 つずつ送るよりも **まとめて送る方が高速**
- **リクエストの頻度を調整**
  - 例えば、`10件以上 or 500ms 経過` したら送る
- **非同期バッファリング**
  - `ConcurrentQueue` などを使い、一定間隔で送信

---

## **4. `ConcurrentQueue` を使って非同期バッファリング**
Loki に **一定時間 or 一定件数ごとにまとめて送る** 仕組みを作ると、さらに効率化できます。

### **C# コード: `ConcurrentQueue` でバッファリング**
```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string lokiUrl = "http://localhost:3100/loki/api/v1/push";
    private static readonly ConcurrentQueue<(DateTimeOffset, string)> logQueue = new();
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);
    private static readonly int BatchSize = 5;

    static async Task Main()
    {
        // バックグラウンドでバッチ送信処理を開始
        _ = Task.Run(ProcessLogQueue);

        // ログを追加
        for (int i = 0; i < 20; i++)
        {
            AddLog($"Log message {i}");
            await Task.Delay(300); // 擬似的なログ間隔
        }

        // 終了前にフラッシュ
        await Task.Delay(FlushInterval);
    }

    static void AddLog(string message)
    {
        logQueue.Enqueue((DateTimeOffset.UtcNow, message));
    }

    static async Task ProcessLogQueue()
    {
        while (true)
        {
            await Task.Delay(FlushInterval);

            if (logQueue.Count == 0) continue;

            var logsToSend = new List<(DateTimeOffset, string)>();

            while (logsToSend.Count < BatchSize && logQueue.TryDequeue(out var log))
            {
                logsToSend.Add(log);
            }

            if (logsToSend.Count > 0)
            {
                await SendLogsToLoki(logsToSend);
            }
        }
    }

    static async Task SendLogsToLoki(List<(DateTimeOffset Timestamp, string Message)> logEntries)
    {
        var logValues = new List<string[]>();

        foreach (var log in logEntries)
        {
            string timestamp = log.Timestamp.ToUnixTimeNanoseconds().ToString();
            logValues.Add(new string[] { timestamp, log.Message });
        }

        var payload = new
        {
            streams = new[]
            {
                new
                {
                    stream = new { job = "csharp-app", level = "info" },
                    values = logValues
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(lokiUrl, content);
        Console.WriteLine($"Response: {response.StatusCode}, Sent {logEntries.Count} logs.");
    }
}
```

---

## **5. さらに高速化するには？**
✅ **`gzip` 圧縮を使う**  
Loki は `gzip` 圧縮をサポートしており、リクエストのサイズを削減できます。
```csharp
content.Headers.ContentEncoding.Add("gzip");
```

✅ **`Persistent Connection` を使う**  
`HttpClient` を使いまわすことで、接続オーバーヘッドを削減できます。

✅ **`Load Balancer` を活用**  
Loki がクラスタ構成なら、`Nginx` や `HAProxy` で負荷分散。

✅ **`Grafana Agent` を使う**
Loki に直接送るのではなく **Grafana Agent** を中継して送ると負荷軽減。

---

## **まとめ**
| 方法 | メリット | デメリット |
|------|---------|-----------|
| **1つずつ送る (`HttpClient`)** | シンプル | 転送効率が悪い |
| **Bulk 送信 (`values` に複数のログをまとめる)** | 転送効率UP | コードの調整が必要 |
| **非同期バッファリング (`ConcurrentQueue`)** | 負荷を分散 | 遅延が発生する可能性 |
| **gzip 圧縮** | 転送データを圧縮 | CPU 使用量が増える |

**おすすめ:**  
- **大量のログを送るなら `Bulk 送信` + `ConcurrentQueue`**
- **負荷を抑えるなら `Grafana Agent` を使う**

これで **Loki へのログ転送の効率を最大化** できます！🚀

### **Loki の `stream` は複数持てるのか？**
はい、**Loki の `streams` 配列には複数の `stream` を含めることができます**。これにより、異なる **ログレベル (`info`, `error`, `debug` など) やアプリケーション (`job`)** ごとにログを整理して送信できます。

---

## **1. Loki に複数の `stream` を送る方法**
Loki の API は、1 回の `POST` リクエストで **複数の `stream` を送信可能** です。

### **JSON の構造**
```json
{
  "streams": [
    {
      "stream": { "job": "csharp-app", "level": "info" },
      "values": [
        ["1710842134567890000", "Info log message 1"],
        ["1710842134567990000", "Info log message 2"]
      ]
    },
    {
      "stream": { "job": "csharp-app", "level": "error" },
      "values": [
        ["1710842134568880000", "Error log message 1"],
        ["1710842134569990000", "Error log message 2"]
      ]
    }
  ]
}
```
**ポイント**
- `streams` 配列に `info` 用と `error` 用の 2 つの `stream` を持たせる
- 各 `stream` は `job` (`csharp-app`) と `level` (`info` や `error`) を持つ
- それぞれの `values` 配列に複数のログを格納

---

## **2. C# で複数の `stream` を含む Bulk 送信**
以下の C# コードでは、`info` と `error` の **2 つのログレベルのログを並列で送信** します。

### **C# コード**
```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string lokiUrl = "http://localhost:3100/loki/api/v1/push";

    static async Task Main()
    {
        var logs = new List<(DateTimeOffset Timestamp, string Level, string Message)>
        {
            (DateTimeOffset.UtcNow, "info", "Info log message 1"),
            (DateTimeOffset.UtcNow.AddMilliseconds(10), "info", "Info log message 2"),
            (DateTimeOffset.UtcNow.AddMilliseconds(20), "error", "Error log message 1"),
            (DateTimeOffset.UtcNow.AddMilliseconds(30), "error", "Error log message 2"),
            (DateTimeOffset.UtcNow.AddMilliseconds(40), "debug", "Debug log message 1")
        };

        await SendLogsToLoki(logs);
    }

    static async Task SendLogsToLoki(List<(DateTimeOffset Timestamp, string Level, string Message)> logEntries)
    {
        // ログレベルごとにグループ化
        var groupedLogs = new Dictionary<string, List<string[]>>();

        foreach (var log in logEntries)
        {
            string timestamp = log.Timestamp.ToUnixTimeNanoseconds().ToString();

            if (!groupedLogs.ContainsKey(log.Level))
                groupedLogs[log.Level] = new List<string[]>();

            groupedLogs[log.Level].Add(new string[] { timestamp, log.Message });
        }

        // Loki 形式に変換
        var streams = new List<object>();

        foreach (var kvp in groupedLogs)
        {
            streams.Add(new
            {
                stream = new { job = "csharp-app", level = kvp.Key },
                values = kvp.Value
            });
        }

        var payload = new { streams };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(lokiUrl, content);
        Console.WriteLine($"Response: {response.StatusCode}");
    }
}
```

---

## **3. コードの解説**
1. **複数のログ (`info`, `error`, `debug`) をリストに追加**
2. **ログレベル (`level`) ごとに `Dictionary` に分類**
   ```csharp
   var groupedLogs = new Dictionary<string, List<string[]>>();
   ```
3. **`Dictionary` の各 `level` ごとに `streams` を作成**
   ```csharp
   streams.Add(new { stream = new { job = "csharp-app", level = kvp.Key }, values = kvp.Value });
   ```
4. **JSON を Loki に送信**
   ```csharp
   var payload = new { streams };
   var json = JsonSerializer.Serialize(payload);
   var content = new StringContent(json, Encoding.UTF8, "application/json");
   await client.PostAsync(lokiUrl, content);
   ```

---

## **4. 出力される JSON（Loki に送信されるデータ）**
```json
{
  "streams": [
    {
      "stream": { "job": "csharp-app", "level": "info" },
      "values": [
        ["1710842134567890000", "Info log message 1"],
        ["1710842134567990000", "Info log message 2"]
      ]
    },
    {
      "stream": { "job": "csharp-app", "level": "error" },
      "values": [
        ["1710842134568880000", "Error log message 1"],
        ["1710842134569990000", "Error log message 2"]
      ]
    },
    {
      "stream": { "job": "csharp-app", "level": "debug" },
      "values": [
        ["1710842134570000000", "Debug log message 1"]
      ]
    }
  ]
}
```

---

## **5. メリット**
✅ **Loki の `stream` を活用し、異なるログレベルを整理**  
✅ **1 回の `POST` で複数の `stream` を送ることで効率向上**  
✅ **ログレベルごとに `Dictionary` で管理することで、拡張しやすい**  
✅ **複数のジョブ (`job`) やアプリケーションに対応しやすい**

---

## **6. さらに応用するには？**
- `job` や `service` などのラベルを追加することで、より細かく分類できる。
- `hostname` などの情報も `stream` に追加可能。
  ```json
  {
    "stream": { "job": "csharp-app", "level": "info", "host": "server-01" }
  }
  ```
- `gzip` 圧縮を活用することで、さらに転送効率を改善。

```csharp
content.Headers.ContentEncoding.Add("gzip");
```

---

## **7. まとめ**
| 方法 | メリット |
|------|---------|
| **単一の `stream` に全ログを送る** | シンプルだが、検索・フィルタが難しい |
| **`stream` をログレベルごとに分割** | Loki のラベル機能を活用しやすい |
| **`job`, `service` もラベルに含める** | マイクロサービスや複数アプリのログ管理に最適 |

**おすすめ:**  
- **ログレベルごとに `stream` を分けるのが Loki の設計に合っている** 🚀  
- **`Dictionary` でグループ化して `streams` にまとめるのがベストプラクティス**

これで **Loki へのログ転送の柔軟性と効率を最大化** できます！🔥