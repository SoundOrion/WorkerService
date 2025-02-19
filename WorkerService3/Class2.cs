C# の `ILogger` を使用して適切なログメッセージを書くためのベストプラクティスを紹介します。

-- -

### 1. **適切なログレベルを選ぶ**
`ILogger` には以下のログレベルがあり、適切なレベルを選ぶことが重要です。

| レベル | 用途 |
|--------|-------------------------|
| `Trace` | 非常に詳細な情報 (デバッグ用) |
| `Debug` | デバッグ情報 |
| `Information` | 一般的な操作の記録 |
| `Warning` | 想定外だがアプリは動作できる状態 |
| `Error` | エラーが発生し、処理が継続できない可能性がある |
| `Critical` | 致命的なエラー (システムクラッシュなど) |

**例:**
```csharp
logger.LogInformation("User {UserId} has logged in at {Time}.", userId, DateTime.UtcNow);
logger.LogWarning("Disk space is running low: {AvailableSpace} GB remaining.", availableSpace);
logger.LogError("Failed to process request {RequestId}. Exception: {ExceptionMessage}", requestId, ex.Message);
```

---

### 2. **構造化ロギングを活用する**
文字列の連結ではなく、**プレースホルダー(`{ }`) を使用してログを記述する** ことで、検索性や分析のしやすさが向上します。

**悪い例 (文字列連結)**
```csharp
logger.LogInformation("User " + userId + " has logged in at " + DateTime.UtcNow);
```

**良い例(構造化ロギング) * *
```csharp
logger.LogInformation("User {UserId} has logged in at {Time}.", userId, DateTime.UtcNow);
```
これは、ログシステムによって構造化データとして保存されるため、後で `UserId` で検索しやすくなります。

---

### 3. **エラー時は例外を含める**
例外 (`Exception`) をキャッチしたときは、`logger.LogError` の第1引数に例外を渡すことで、スタックトレースもログに記録できます。

```csharp
try
{
    // 何らかの処理
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while processing the request.");
}
```

---

### 4. **コンテキスト情報を含める**
アプリケーションの状態やリクエスト情報など、ログメッセージだけではわからないコンテキスト情報を含めると、デバッグや分析が容易になります。

**例 (リクエスト情報を含める)**
```csharp
logger.LogInformation("Request {RequestId} started by User {UserId}.", requestId, userId);
```

---

### 5. **機密情報をログに出力しない**
パスワードやクレジットカード情報など、**機密情報をログに含めない * *ように注意します。

**悪い例(パスワードを記録する) * *
```csharp
logger.LogWarning("User {UserId} attempted to log in with password {Password}.", userId, password);
```

**良い例(パスワードを記録しない) * *
```csharp
logger.LogWarning("User {UserId} attempted to log in.", userId);
```

---

### 6. **ログメッセージの一貫性を保つ**
異なるチームメンバーがログを記述する場合、フォーマットが統一されていると可読性が向上します。

**統一されたフォーマットの例**
- `"{Action} - {Details}"`
- `"[{Module}] {Message}"`

**例 (統一されたログメッセージ)**
```csharp
logger.LogInformation("[Auth] User {UserId} logged in.");
logger.LogError("[Database] Failed to update record {RecordId}. Exception: {ExceptionMessage}", recordId, ex.Message);
```

---

### 7. **カテゴリーロガーを使用する**
`ILogger < T >` を使用すると、クラスごとに適切なカテゴリが付与され、どのコンポーネントからのログかが分かりやすくなります。

```csharp
public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void Login(string userId)
    {
        _logger.LogInformation("User {UserId} logged in.", userId);
    }
}
```

これにより、ログに `CategoryName="Namespace.UserService"` のような情報が追加されます。

---

### 8. **適切なログのフィルタリングを設定する**
`appsettings.json` でログレベルを制御することで、**本番環境では `Information` 以上、開発環境では `Debug` 以上** のログを出力するように設定できます。

```json
{
  "Logging": {
    "LogLevel": {
        "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
}
}
```

開発環境では `Debug` 以上、本番環境では `Information` 以上を出力するようにするのが一般的です。

---

## まとめ
| ベストプラクティス | 説明 |
|------------------|--------------------------------|
| **適切なログレベルを選ぶ** | `Information`, `Warning`, `Error` など用途に応じたレベルを使う |
| **構造化ロギングを使う** | プレースホルダー `{}` を活用して検索しやすくする |
| **例外情報を記録する * * | `logger.LogError(ex, "...")` でスタックトレースを含める |
| **機密情報を含めない * * | パスワードなどは記録しない |
| **一貫したフォーマットを使う * * | `"[{Module}] {Message}"` のように統一する |
| **カテゴリーロガーを使う * * | `ILogger < T >` でコンポーネントごとにロガーを分ける |
| **環境ごとにログレベルを設定する * * | `appsettings.json` でフィルタリングを設定する |

これらを意識すると、保守性が高く、分析しやすいログが記録できます！