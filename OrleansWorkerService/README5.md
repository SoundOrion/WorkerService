Orleans を使ってこの NATS のメッセージングシステムを **分散アクターモデル** にパワーアップします。  
Orleans の最新バージョン（**Orleans 7**）を使用し、以下の改善を加えます。

---

### **パワーアップのポイント**
✅ **スケール可能なアーキテクチャ**  
　Orleans の **Grain（仮想アクター）** を活用し、部屋ごと・ユーザーごとにアクターを作成。  

✅ **永続化 & 状態管理**  
　Orleans の **Stateful Grain** を活用し、受信したメッセージの履歴を保存可能。  

✅ **分散クラスタ対応**  
　複数の Orleans ノードを立ち上げることで、システム全体をスケールアウト可能。  

✅ **NATSとの統合**  
　Orleans の **Grain 通信** + **NATSのPub/Sub** を組み合わせ、より堅牢なメッセージングシステムを実現。  

---

## **構成**
- **`IChatRoomGrain` (Grainインターフェース)**  
　部屋ごとのチャット管理アクター。
- **`ChatRoomGrain` (Grain実装)**  
　Orleans の Stateful Grain を使用し、メッセージ履歴を保持。
- **`IUserGrain` (Grainインターフェース)**  
　ユーザーごとのメッセージ送受信管理アクター。
- **`UserGrain` (Grain実装)**  
　NATS との通信を行い、メッセージを Orleans クラスタにブロードキャスト。
- **`Program.cs` (エントリーポイント)**  
　Orleans クラスタのホストとして動作。

---

## **Orleans + NATS のコード**
### **1. Orleans の Grain インターフェース**
#### **`IChatRoomGrain.cs`**
```csharp
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IChatRoomGrain : IGrainWithStringKey
{
    Task SendMessage(string user, string message);
    Task<List<string>> GetChatHistory();
}
```

---

#### **`IUserGrain.cs`**
```csharp
using Orleans;
using System.Threading.Tasks;

public interface IUserGrain : IGrainWithStringKey
{
    Task SendMessage(string message);
}
```

---

### **2. Orleans の Grain 実装**
#### **`ChatRoomGrain.cs`**
```csharp
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ChatRoomGrain : Grain, IChatRoomGrain
{
    private readonly List<string> _chatHistory = new();

    public Task SendMessage(string user, string message)
    {
        var fullMessage = $"{user}: {message}";
        _chatHistory.Add(fullMessage);
        Console.WriteLine($"[ChatRoom] {fullMessage}");
        return Task.CompletedTask;
    }

    public Task<List<string>> GetChatHistory()
    {
        return Task.FromResult(_chatHistory);
    }
}
```

---

#### **`UserGrain.cs`**
```csharp
using Orleans;
using NATS.Client;
using System;
using System.Threading.Tasks;

public class UserGrain : Grain, IUserGrain
{
    private IConnection _natsConnection;

    public override async Task OnActivateAsync()
    {
        var cf = new ConnectionFactory();
        _natsConnection = cf.CreateConnection("nats://demo.nats.io");
        Console.WriteLine($"[UserGrain] NATS Connected for {this.GetPrimaryKeyString()}");
    }

    public async Task SendMessage(string message)
    {
        var chatRoom = GrainFactory.GetGrain<IChatRoomGrain>("general");
        await chatRoom.SendMessage(this.GetPrimaryKeyString(), message);
        _natsConnection.Publish($"chat.general", System.Text.Encoding.UTF8.GetBytes($"{this.GetPrimaryKeyString()}: {message}"));
    }

    public override Task OnDeactivateAsync()
    {
        _natsConnection?.Dispose();
        return Task.CompletedTask;
    }
}
```

---

### **3. Orleans クラスタのエントリーポイント**
#### **`Program.cs`**
```csharp
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

var host = Host.CreateDefaultBuilder()
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering(); // Orleansクラスタをローカルで実行
    })
    .Build();

await host.RunAsync();
```

---

### **4. クライアント側（NATSを通じてOrleansにメッセージ送信）**
#### **`Client.cs`**
```csharp
using Orleans;
using Orleans.Hosting;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new ClientBuilder()
            .UseLocalhostClustering()
            .Build();

        await client.Connect();

        Console.Write("Enter your name: ");
        var userName = Console.ReadLine();
        var userGrain = client.GetGrain<IUserGrain>(userName);

        while (true)
        {
            Console.Write("Enter a message: ");
            var message = Console.ReadLine();
            await userGrain.SendMessage(message);
        }
    }
}
```

---

## **実行方法**
1. Orleans クラスタのホストを起動
   ```sh
   dotnet run --project OrleansHost
   ```
2. クライアントを起動（複数ターミナルで）
   ```sh
   dotnet run --project OrleansClient
   ```
3. **ユーザーがメッセージを送信すると、NATS経由でOrleansのチャットルームにブロードキャスト！**

---

## **Orleans + NATS のメリット**
🔹 **完全非同期 & スケーラブル**
   - Orleans の **仮想アクター** を活用し、クラスタ全体で負荷分散が可能。

🔹 **リアルタイム メッセージング**
   - Orleans **Grain通信** + **NATSのPub/Sub** により、リアルタイムメッセージ送信が可能。

🔹 **ステートフルなチャットルーム**
   - Orleans **Stateful Grain** により、チャット履歴を永続化可能。

🔹 **分散システムでも一貫した状態管理**
   - Orleans の **仮想アクターモデル** により、ノードが増減してもシステムが崩壊しない。

---

## **次のステップ**
- **クラウドデプロイ**（Azure, AWS, Kubernetes）
- **Orleansの永続化ストレージ（Azure Table, MongoDB, PostgreSQL）**
- **NATS JetStreamを活用してメッセージを保存**
- **Webフロントエンド（Blazor, React）と連携**
- **認証機能の追加（JWT, Identity Server）**

この Orleans 強化版、試してみますか？ 🚀