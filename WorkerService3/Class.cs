using System.Net.Mail;

以下に `IAlertService` の実装を作成します。  
**基本的なアラートの送信方法として、以下の 3 つを実装します。**
1. **コンソール通知 (`ConsoleAlertService`)**  
2. **メール通知 (`EmailAlertService`)**  
3. **Slack / Discord / Webhook (`WebhookAlertService`)**

---

## **1. `IAlertService` インターフェース**
まず、アラートを送信するための共通インターフェース `IAlertService` を定義します。

```csharp
public interface IAlertService
{
    Task SendAlertAsync(string message);
}
```

---

## **2. `ConsoleAlertService`（シンプルなコンソール通知）**
コンソールにアラートを表示するシンプルな実装。

```csharp
public class ConsoleAlertService : IAlertService
{
    public Task SendAlertAsync(string message)
    {
        Console.WriteLine($"[ALERT] {message}");
        return Task.CompletedTask;
    }
}
```
**📌 実装のポイント * *
-**シンプルにコンソールにメッセージを出力 * *するだけのアラートサービス
- **非同期メソッド(`Task.CompletedTask`) を返す** ことで、インターフェースに準拠

---

## **3. `EmailAlertService`（SMTP を使用したメール通知）**
**📌 メール送信には `SmtpClient` を使用（SMTP サーバーが必要）**

```csharp
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public class EmailAlertService : IAlertService
{
    private readonly string _smtpServer = "smtp.example.com"; // SMTP サーバーのアドレス
    private readonly int _smtpPort = 587; // ポート番号
    private readonly string _fromEmail = "alert@example.com"; // 送信元メールアドレス
    private readonly string _toEmail = "admin@example.com"; // 送信先メールアドレス
    private readonly string _smtpUser = "your_smtp_username"; // SMTP ユーザー
    private readonly string _smtpPassword = "your_smtp_password"; // SMTP パスワード

    public async Task SendAlertAsync(string message)
    {
        using (var client = new SmtpClient(_smtpServer, _smtpPort))
        {
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);
            client.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = "Server Connection Alert",
                Body = message,
                IsBodyHtml = false
            };

            mailMessage.To.Add(_toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
                Console.WriteLine("Alert email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send alert email: {ex.Message}");
            }
        }
    }
}
```
**📌 実装のポイント * *
-**SMTP サーバーを使用してメール送信 * *
-**認証(`NetworkCredential`) を設定 * *
-**`await client.SendMailAsync()` を使用し、非同期で送信**

---

## **4. `WebhookAlertService`（Slack / Discord / Teams などの Webhook 通知）**
Slack や Discord、Microsoft Teams の **Webhook** を使って通知を送信。

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class WebhookAlertService : IAlertService
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl = "https://hooks.slack.com/services/your-webhook-url";

    public WebhookAlertService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendAlertAsync(string message)
    {
        var payload = new { text = message }; // Slack 用の JSON データ
        var jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Alert sent successfully to Webhook.");
            }
            else
            {
                Console.WriteLine($"Failed to send alert. HTTP Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending webhook alert: {ex.Message}");
        }
    }
}
```
**📌 実装のポイント * *
-**Slack / Discord / Teams の Webhook URL を使って通知を送信**
- **`HttpClient` を使い、JSON データを `POST` する**
- **Webhook の形式に合わせて `payload` を変更可能**

---

## **5. DI（依存性注入）を設定**
`Program.cs` で `IAlertService` の DI 設定を追加します。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IAlertService, EmailAlertService>(); // メール通知を使用する
                                                                           // services.AddSingleton<IAlertService, WebhookAlertService>(); // Webhook を使用する場合
                                                                           // services.AddSingleton<IAlertService, ConsoleAlertService>(); // コンソール通知のみ

                services.AddSingleton<HttpClient>(); // Webhook 用に HttpClient を注入
                services.AddSingleton<IConnectionService, ConnectionService>();
                services.AddHostedService<ConnectionBackgroundService>();
            })
            .Build();

        Console.WriteLine("Application started. Press Ctrl+C to exit.");
        await host.RunAsync();
    }
}
```
**📌 DI 設定のポイント**
- **メール / Webhook / コンソールのいずれかを選択して `IAlertService` に登録**
- **`HttpClient` を `WebhookAlertService` に渡すため `AddSingleton<HttpClient>()` を追加**

---

## **6. `ConnectionService` にアラート機能を統合**
サーバーがダウンした際に `IAlertService` を呼び出し、通知を送る。

```csharp
public class ConnectionService : IConnectionService
{
    private readonly IAlertService _alertService;

    public ConnectionService(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public async Task MonitorConnectionAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsConnected)
            {
                await _alertService.SendAlertAsync("Server connection lost! Trying to reconnect...");
            }
            await Task.Delay(10000, cancellationToken);
        }
    }
}
```
**💡 これで、サーバーが落ちると `IAlertService` を通じてアラートが送信される！**

---

## **7. まとめ**
| アラート方式 | メリット | 実装クラス |
|-------------|---------|------------|
| **コンソール通知** | シンプルでデバッグ向き | `ConsoleAlertService` |
| **メール通知** | 管理者向けの重要な通知 | `EmailAlertService` |
| **Slack / Discord / Teams Webhook** | チーム向け通知に最適 | `WebhookAlertService` |

✅ **サーバーがダウンすると自動でアラートが送られる！**  
✅ **DI を活用して柔軟に通知方法を切り替え可能！**  
✅ **これで、常駐アプリが自動でトラブルを報告できるようになる！** 🚀


### **MailKit を使用した `EmailAlertService` の実装**
確かに、`SmtpClient` は **.NET 6 以降では非推奨** となり、代わりに **MailKit** を使うのが推奨されています。

### **MailKit を使用する理由**
✅ **`SmtpClient` よりも非同期に特化**  
✅ **OAuth などの高度な認証にも対応**  
✅ **.NET の公式ドキュメントでも推奨**  

---

## **1. MailKit のインストール**
MailKit は NuGet からインストールできます。

```sh
dotnet add package MailKit
```

または、Visual Studio の NuGet パッケージマネージャーから **MailKit** を検索してインストールしてください。

---

## **2. `EmailAlertService` の実装**
MailKit の `SmtpClient` を使って **メールを送信するアラートサービス** を実装します。

```csharp
using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

public class EmailAlertService : IAlertService
{
    private readonly string _smtpServer = "smtp.example.com"; // SMTP サーバー
    private readonly int _smtpPort = 587; // SMTPポート (25, 465, 587)
    private readonly string _smtpUser = "your_smtp_username"; // SMTPユーザー
    private readonly string _smtpPassword = "your_smtp_password"; // SMTPパスワード
    private readonly string _fromEmail = "alert@example.com"; // 送信元
    private readonly string _toEmail = "admin@example.com"; // 送信先

    public async Task SendAlertAsync(string message)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Alert System", _fromEmail));
            email.To.Add(new MailboxAddress("Admin", _toEmail));
            email.Subject = "Server Connection Alert";
            email.Body = new TextPart("plain")
            {
                Text = message
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtpUser, _smtpPassword);
            await client.SendAsync(email);
            await client.DisconnectAsync(true);

            Console.WriteLine("Alert email sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send alert email: {ex.Message}");
        }
    }
}
```

---

## **3. 実装のポイント**
✅ **`MimeKit` を使って、HTML メールや添付ファイルも送信可能**  
✅ **MailKit の `SmtpClient` は `ConnectAsync()` で非同期に接続可能**  
✅ **OAuth2 認証も可能（Google, Microsoft 365 など）**  

---

## **4. MailKit 版と SmtpClient 版の違い**
| 機能 | `System.Net.Mail (SmtpClient)` | `MailKit` |
|------|-----------------|---------|
| **サポート状況** | 非推奨 (.NET 6 以降) | 推奨 |
| **非同期対応** | 弱い (`SendMailAsync` は実装が同期的) | 強力な `ConnectAsync` `SendAsync` |
| **OAuth2 認証** | なし | あり（Google, Microsoft 365） |
| **TLS / SSL** | 基本対応 | 柔軟なオプション (`Auto`, `StartTLS`, `SSL`) |
| **HTML メール / 添付** | 可能 | より柔軟 |

---

## **5. DI に登録**
`Program.cs` で `IAlertService` の DI 設定を `EmailAlertService` にする。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IAlertService, EmailAlertService>(); // MailKit 版のメール通知を使用
                services.AddSingleton<IConnectionService, ConnectionService>();
                services.AddHostedService<ConnectionBackgroundService>();
            })
            .Build();

        Console.WriteLine("Application started. Press Ctrl+C to exit.");
        await host.RunAsync();
    }
}
```

---

## **6. さらに改善するポイント**
1. **OAuth2 を使用**
   - Google, Microsoft 365 では SMTP パスワード認証が制限されているため、OAuth2 認証が必要。
   - `SaslMechanismOAuth2` を使って MailKit で OAuth2 認証が可能。

2. **HTML メールを送信**
   ```csharp
   email.Body = new TextPart("html")
   {
       Text = "<h1>Server Connection Alert</h1><p>The server is down!</p>"
   };
   ```
   - HTML メールで視認性を向上。

3. **複数の宛先に通知**
   ```csharp
   email.To.Add(new MailboxAddress("Admin2", "admin2@example.com"));
   ```

---

## **7. まとめ**
✅ `MailKit` を使用することで **非同期 & 高機能なメール送信が可能**  
✅ **OAuth2 認証にも対応できる** ので、Gmail や Outlook でも使用可能  
✅ **SMTP 設定を `appsettings.json` から取得すると、より柔軟に管理できる**  

💡 **MailKit を使った `EmailAlertService` を実装すれば、.NET の推奨する方法で安定したメール通知が可能になります！** 🚀


機能	理由	方法
✅ ログ記録	問題発生時に原因を追跡	ILogger を利用
✅ アラート通知	サーバーダウン時に管理者へ通知	IAlertService を実装（Email/Slack）
✅ メモリ & CPU 監視	メモリリークやCPU過負荷を検出	System.Diagnostics を使用
✅ 自動リスタート	アプリのクラッシュ対策	Windows タスクスケジューラ or PowerShell
✅ 動的設定更新	設定変更を反映しやすくする	IOptions<T> を使う



## **イベントログを監視してアプリのクラッシュを検知し、自動再起動**
Windows は、アプリケーションが **クラッシュすると `Event Viewer（イベントビューアー）` にログを記録** します。  
このログを監視することで、アプリがクラッシュしたことを **正確に検知** し、**自動で再起動** できます。

---

## **1. イベントログの確認方法**
まず、**アプリがクラッシュした際のイベント ID を特定** します。

### **📌 イベントビューアーで確認**
1. `Win + R` を押して `eventvwr.msc` を入力し、Enter
2. 左ペインで **「Windows ログ」 → 「アプリケーション」** を開く
3. `ソース` 列で **`Application Error`** を探す
4. `イベント ID` を確認（通常 `1000`）

---

## **2. PowerShell を使ってイベントログを監視し、クラッシュ時に再起動**
Windows の **`Get-WinEvent`** コマンドで `イベント ID: 1000` を監視し、**アプリがクラッシュしたら再起動** する。

### **📌 `monitor-eventlog.ps1`（PowerShell スクリプト）**
```powershell
$AppName = "MyApp.exe"
$AppPath = "C:\Program Files\MyApp\MyApp.exe"

$EventLogFilter = @{
    LogName   = 'Application'
    Id        = 1000 # アプリケーションエラー（クラッシュ）
    Newest    = 1
}

while ($true) {
    $Event = Get-WinEvent -FilterHashtable $EventLogFilter -ErrorAction SilentlyContinue

    if ($Event -ne $null -and $Event.Message -match $AppName) {
        Write-Host "[$(Get-Date)] $AppName クラッシュ検出！再起動中..."
        Start-Process -FilePath $AppPath
    }

    Start-Sleep -Seconds 10
}
```

### **📌 実行方法**
1. `monitor-eventlog.ps1` を適当なフォルダに保存（例: `C:\scripts\monitor-eventlog.ps1`）
2. **PowerShell スクリプトの実行を許可**
   ```powershell
   Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```
3. **タスクスケジューラで 1 分ごとに実行**
   - `Program/script` に `powershell`
   - `Arguments` に `-File "C:\scripts\monitor-eventlog.ps1"`

---

## **3. イベントログ監視をタスクスケジューラに設定（自動化）**
PowerShell を **タスクスケジューラで定期実行すれば、イベントログを監視し続けることができる**。

### **📌 設定手順**
1. `Win + R` → `taskschd.msc` でタスクスケジューラを開く
2. `タスクの作成` をクリック
3. **「全般」タブ**
   - 名前: `Monitor MyApp Crash`
   - `最上位の特権で実行する` にチェックを入れる
4. **「トリガー」タブ**
   - `新規` → `タスクの開始: ログにイベントが記録されたとき`
   - `ログ: Application`
   - `ソース: Application Error`
   - `イベント ID: 1000`
5. **「操作」タブ**
   - `新規` → `プログラムの開始`
   - `プログラム/スクリプト` → `powershell`
   - `引数の追加` → `-File "C:\scripts\monitor-eventlog.ps1"`

### **✅ 完了！**
これで、**アプリがクラッシュすると自動的にスクリプトが実行され、アプリが再起動する！** 🚀

---

## **4. どの方法を選ぶべきか？**
| 方法 | メリット | デメリット |
|------|---------|----------|
| **タスクスケジューラで定期実行** | ✅ シンプルで確実 | ❌ クラッシュ直後に即時再起動できない |
| **PowerShell でイベントログを監視** | ✅ クラッシュ直後に即時再起動 | ❌ PowerShell がバックグラウンドで実行される |
| **Windows サービス化** | ✅ OS による自動再起動 | ❌ サービス登録が必要 |

### **💡 推奨方法**
🚀 **「タスクスケジューラでイベントログ監視」が最も安全で確実！**  
🎯 **できない場合は「PowerShell で定期的に監視」**  

✅ **これで、アプリがクラッシュしても自動で復旧できる！** 🎉




### **OS 側でアプリのクラッシュを検知し、プッシュ再起動する方法**
Windows **OS 側からアプリのクラッシュを検知して再起動する方法** を解説します。  
以下の 2 つの方法があり、どちらも **OS 側でプッシュしてアプリを再起動** できます。

---

## **1. Windows タスクスケジューラを使う（イベントログ監視 + 自動再起動）**
Windows **イベントログ（Event Viewer）に記録されたクラッシュログをトリガーに、アプリを自動再起動する方法** です。

✅ **OS 側がプッシュでアプリのクラッシュを検知し、即時再起動が可能**  
✅ **アプリの異常終了を検出し、OS がリカバリーを管理**

---

### **📌 手順**
1. **タスクスケジューラを開く**
   - `Win + R` を押して **`taskschd.msc`** と入力 → `Enter`
   - 左側メニューの `タスク スケジューラ ライブラリ` を選択
   - `タスクの作成` をクリック

2. **「全般」タブで設定**
   - 名前: **`AutoRestart MyApp`**
   - `最上位の特権で実行する`（管理者権限で実行）

3. **「トリガー」タブ**
   - `新規` をクリックし、以下の設定を追加:
     - `タスクの開始` → **`ログにイベントが記録されたとき`**
     - `ログ:` **`Application`**
     - `ソース:` **`Application Error`**
     - `イベント ID:` **`1000`**（アプリケーションエラー）

4. **「操作」タブ**
   - `新規` → `プログラムの開始`
   - `プログラム/スクリプト` → **`C:\Program Files\MyApp\MyApp.exe`**

5. **「条件」タブ**
   - `コンピューターが AC 電源で動作している場合のみタスクを開始する` のチェックを外す（バッテリーでも実行）

6. **「設定」タブ**
   - `タスクがすでに実行中の場合、新しいインスタンスを開始しない` に設定
   - `タスクが失敗した場合、再起動を試みる` → **`1 分後に再起動する（再試行 3 回）`**

---

### **✅ 完了！**
Windows OS が **クラッシュを検知すると、即座にアプリを再起動** します。

---

## **2. Windows サービスとして登録（最も安全な方法）**
常駐アプリを **Windows サービスとして登録すると、OS による自動再起動が可能** になります。

### **📌 メリット**
✅ OS による **プロセス管理 & 自動再起動**  
✅ **サービスの管理が簡単**（`sc start/stop MyAppService`）  
✅ **異常終了後、即座に自動復旧** できる  

---

### **📌 Windows サービス化の方法**
1. **C# アプリを Windows サービス用に変更**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MyService : BackgroundService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service is running...");
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Application is alive...");
            await Task.Delay(10000, stoppingToken); // 10秒ごとに動作
        }
    }
}
```

2. **Windows サービスとして登録**
   - `Program.cs`
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseWindowsService() // Windows サービスとして実行
            .ConfigureServices(services =>
            {
                services.AddHostedService<MyService>();
            })
            .Build();

        await host.RunAsync();
    }
}
```

3. **サービスをインストール**
   - `sc create MyAppService binPath= "C:\path\to\MyApp.exe" start= auto`
   - `sc start MyAppService`

4. **サービスがクラッシュしても自動再起動**
   ```powershell
   sc failure MyAppService reset= 86400 actions= restart/60000
   ```
   - **異常終了後 60 秒後に自動再起動**
   - **リセットは 1 日（86400 秒）後にリセット**

---

### **✅ 完了！**
Windows サービスとして登録することで、  
**OS が自動的にプロセスを監視し、異常終了したら即座に再起動** できます。

---

## **3. どの方法を選ぶべきか？**
| 方法 | メリット | デメリット |
|------|---------|----------|
| **タスクスケジューラ + イベントログ監視** | ✅ 簡単に実装 ✅ OS がクラッシュを検知して即時再起動 | ❌ Windows タスクスケジューラの設定が必要 |
| **Windows サービス化** | ✅ OS が完全に管理 ✅ 自動再起動 ✅ Windows の公式な手法 | ❌ GUI アプリには向かない |

---

## **4. まとめ**
✅ **「Windows タスクスケジューラでイベントログ監視」が簡単 & 確実！**  
✅ **「Windows サービス化」すると、OS による完全なプロセス管理が可能！**  

🚀 **どちらの方法も、OS 側からプッシュでアプリの異常終了を検知し、即時再起動が可能！** 🎉



### **アプリが落ちた時のイベントログ ID は 1000 だけ？**
**いいえ、1000 以外にもアプリのクラッシュを示すイベント ID があります！**  
Windows のイベントログには、アプリの異常終了を示すさまざまな **イベント ID** があります。

---

## **1. よく使われるアプリクラッシュ関連のイベント ID**
| イベント ID | 説明 | 備考 |
|------------|------|------|
| **1000** | アプリケーションエラー（一般的なクラッシュ） | **最もよく使われる** |
| **1001** | Windows エラー報告（WER）によるクラッシュ情報 | 追加情報あり |
| **1002** | 応答なし（ハングアップ）したアプリケーション | フリーズ検出 |
| **7026** | サービスまたはドライバがロードに失敗 | OS 側の問題 |
| **7031** | サービスが異常終了 | **Windows サービスの場合に重要** |

---

## **2. どのイベント ID を監視すべき？**
対象のアプリが **通常の EXE（常駐アプリ）** なら → **1000、1001、1002** を監視  
対象のアプリが **Windows サービス** なら → **7031** も監視

---

## **3. Windows イベントビューアーで ID を確認する**
### **📌 手順**
1. `Win + R` → `eventvwr.msc` を実行
2. **「Windows ログ」 → 「アプリケーション」** を開く
3. `ソース` 列で **`Application Error` や `Windows Error Reporting`** を探す
4. **`イベント ID`** を確認する

---

## **4. PowerShell を使って 1000, 1001, 1002 のログを監視して自動再起動**
Windows の **イベントログを監視して、クラッシュ時に自動で再起動** するスクリプトを作成します。

### **📌 `monitor-crash.ps1`**
```powershell
$AppName = "MyApp.exe"
$AppPath = "C:\Program Files\MyApp\MyApp.exe"

$EventLogFilter = @{
    LogName   = 'Application'
    Id        = 1000, 1001, 1002 # クラッシュ & ハングアップの ID
    Newest    = 1
}

while ($true) {
    $Event = Get-WinEvent -FilterHashtable $EventLogFilter -ErrorAction SilentlyContinue

    if ($Event -ne $null -and $Event.Message -match $AppName) {
        Write-Host "[$(Get-Date)] $AppName クラッシュ検出！再起動中..."
        Start-Process -FilePath $AppPath
    }

    Start-Sleep -Seconds 10
}
```

---

## **5. タスクスケジューラを使って OS 側から監視 & 自動再起動**
**PowerShell を使わずに、Windows のタスクスケジューラだけでクラッシュを検知してアプリを再起動** する方法。

### **📌 設定手順**
1. **タスクスケジューラを開く**
   - `Win + R` → `taskschd.msc` を実行
   - `タスクの作成` をクリック

2. **「全般」タブ**
   - 名前: `Monitor MyApp Crash`
   - `最上位の特権で実行する`（管理者権限）

3. **「トリガー」タブ**
   - `新規` → `タスクの開始: ログにイベントが記録されたとき`
   - `ログ:` **`Application`**
   - `ソース:` **`Application Error`, `Windows Error Reporting`**
   - `イベント ID:` **`1000, 1001, 1002`**

4. **「操作」タブ**
   - `新規` → `プログラムの開始`
   - `プログラム/スクリプト` → **`C:\Program Files\MyApp\MyApp.exe`**

---

## **6. まとめ**
✅ **アプリのクラッシュは `1000` だけではなく、`1001`, `1002` も監視すると確実！**  
✅ **Windows イベントビューアーでアプリの異常終了を調べ、適切な ID を監視する！**  
✅ **PowerShell スクリプト or タスクスケジューラで OS 側から自動再起動を実装できる！**  

🚀 **これで、クラッシュを正確に検知し、OS 側からプッシュでアプリを復旧できる！** 🎉