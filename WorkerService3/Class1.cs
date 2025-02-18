🔥 **めちゃくちゃイケてる！**🔥  

✅ **OS 側でクラッシュを正確に検知（イベントログ監視）**  
✅ **タスクスケジューラが `restart-app.ps1` を実行し、リカバリー処理を実施**  
✅ **通知（メール / Slack / Discord）を送信しつつ、アプリを再起動**  
✅ **ログに記録し、障害履歴を管理可能**  

**💡 これなら、OS がアプリのクラッシュを即座に検知してリカバリーを実行しつつ、管理者に通知する "完全自動化システム" が実現できる！** 🚀  

---
## **この方法が最強な理由**
| 方法 | メリット | デメリット |
|------|---------|----------|
| **単に `MyApp.exe` を直接実行** | ✅ シンプル | ❌ 通知やログがない |
| **PowerShell で監視しながら再起動** | ✅ 柔軟な通知や処理が可能 | ❌ PowerShell が常駐する |
| **タスクスケジューラ + `restart-app.ps1`（この方法）** | ✅ **OS 側のイベントログをトリガー** ✅ **通知も記録も自動化** ✅ **プロセスが常駐せず、リソース消費なし！** | **なし！** 🎉 |

---
## **さらにカスタマイズするなら？**
### **✅ (1) Windows サービスの異常終了も監視**
**対象が Windows サービスなら `イベント ID: 7031` も監視対象に追加。**
```powershell
$EventLogFilter = @{
    LogName   = 'System'
    Id        = 7031, 1000, 1001, 1002
    Newest    = 1
}
```

### **✅ (2) 再起動回数をカウントし、一定回数を超えたら手動対応**
**アプリが 3 回連続でクラッシュしたら、再起動せずにアラートだけ送る。**
```powershell
$RestartCountFile = "C:\logs\restart-count.txt"

if (Test-Path $RestartCountFile) {
    $RestartCount = [int] (Get - Content $RestartCountFile)
} else
{
    $RestartCount = 0
}

$RestartCount++
Set - Content - Path $RestartCountFile - Value $RestartCount

if ($RestartCount -gt 3) {
    Send-EmailAlert -Subject "MyApp 再起動ループ検出!" -Message "3 回以上クラッシュしています。手動確認が必要です。"
    Write-Host "再起動を停止。管理者に通知しました。"
    exit
}

Start - Process - FilePath $AppPath
```

### **✅ (3) イベントビューアーのログをカスタム解析**
**エラーメッセージを `restart-app.ps1` の通知メッセージに含める**
```powershell
$ErrorDetails = $Event.Message
$LogMessage = "[$Timestamp] アプリがクラッシュしました。詳細: $ErrorDetails"
Send-EmailAlert -Subject "MyApp クラッシュ通知" -Message $LogMessage
```

---
## **🚀 これが "OS 側のプッシュ通知 + 自動復旧" の完成形！**
**「落ちたら勝手に再起動する」だけではなく、**  
**「通知・ログ・回数制限付きのリカバリー」で、運用負担をゼロに！**  

💡 **この方法なら、**  
「アプリの安定運用」＋「障害通知」＋「完全自動リカバリー」が **OS レベルで実現可能！** 🎉



    C# のコンソールアプリで **クラッシュ監視 + アプリ起動 + 通知機能** を実装します！  
以下の機能を組み込み、Windows タスクスケジューラで **OS 側のイベントログ監視をトリガーに自動実行** します。

---
## **📌 機能一覧**
✅ **Windows イベントログを読み取り、クラッシュを検知（ID 1000, 1001, 1002, 7031）**  
✅ **クラッシュを検知したら `MyApp.exe` を自動起動**  
✅ **メール & Slack / Discord へ通知**  
✅ **クラッシュ回数を記録し、一定回数以上の再起動ループを防ぐ**  
✅ **ログファイルに再起動履歴を記録**

---
## **1. C# で実装**
### **📌 `Program.cs`（監視・起動・通知を統合）**
```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private const string AppName = "MyApp.exe";
    private const string AppPath = @"C:\Program Files\MyApp\MyApp.exe";
    private const string LogPath = @"C:\logs\myapp-restart.log";
    private const string RestartCountPath = @"C:\logs\restart-count.txt";
    private const int MaxRestarts = 3;

    static async Task Main()
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] クラッシュ監視を開始...");

        // クラッシュ回数チェック
        int restartCount = GetRestartCount();
        if (restartCount >= MaxRestarts)
        {
            string stopMessage = $"[{timestamp}] 再起動回数が {MaxRestarts} 回を超えました。手動対応が必要です。";
            Log(stopMessage);
            await SendEmailAsync("MyApp 再起動ループ警告", stopMessage);
            await SendSlackAsync(stopMessage);
            return;
        }

        // イベントログをチェックしてクラッシュを検知
        if (CheckForCrash())
        {
            // 通知を送信
            string crashMessage = $"[{timestamp}] {AppName} がクラッシュしました。再起動します。";
            Log(crashMessage);
            await SendEmailAsync("MyApp クラッシュ通知", crashMessage);
            await SendSlackAsync(crashMessage);

            // アプリを再起動
            RestartApp();

            // 再起動回数を更新
            UpdateRestartCount(restartCount + 1);
        }
    }

    // クラッシュイベントをチェックする
    static bool CheckForCrash()
    {
        EventLog eventLog = new EventLog("Application");
        foreach (EventLogEntry entry in eventLog.Entries)
        {
            if ((entry.InstanceId == 1000 || entry.InstanceId == 1001 || entry.InstanceId == 1002 || entry.InstanceId == 7031) &&
                entry.Message.Contains(AppName))
            {
                return true;
            }
        }
        return false;
    }

    // アプリを再起動
    static void RestartApp()
    {
        Process.Start(AppPath);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {AppName} を再起動しました。");
    }

    // 再起動回数を取得
    static int GetRestartCount()
    {
        if (File.Exists(RestartCountPath))
        {
            int.TryParse(File.ReadAllText(RestartCountPath), out int count);
            return count;
        }
        return 0;
    }

    // 再起動回数を更新
    static void UpdateRestartCount(int count)
    {
        File.WriteAllText(RestartCountPath, count.ToString());
    }

    // ログ記録
    static void Log(string message)
    {
        File.AppendAllText(LogPath, message + Environment.NewLine);
        Console.WriteLine(message);
    }

    // メール通知
    static async Task SendEmailAsync(string subject, string body)
    {
        try
        {
            using (SmtpClient client = new SmtpClient("smtp.example.com", 587))
            {
                client.Credentials = new System.Net.NetworkCredential("your_email@example.com", "your_password");
                client.EnableSsl = true;

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress("alert@example.com"),
                    Subject = subject,
                    Body = body
                };
                mail.To.Add("admin@example.com");

                await client.SendMailAsync(mail);
                Console.WriteLine("メール通知を送信しました。");
            }
        }
        catch (Exception ex)
        {
            Log($"メール通知の送信に失敗: {ex.Message}");
        }
    }

    // Slack / Discord 通知
    static async Task SendSlackAsync(string message)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                var payload = new { text = message };
                var jsonPayload = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("https://hooks.slack.com/services/your/webhook/url", jsonPayload);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Slack 通知を送信しました。");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Slack 通知の送信に失敗: {ex.Message}");
        }
    }
}
```

---

## **2. Windows タスクスケジューラで実行**
1. * *タスクスケジューラを開く * *
   - `Win + R` → `taskschd.msc` を実行
   - `タスクの作成` をクリック

2. **「全般」タブ**
   - 名前: `Monitor MyApp Crash`
   - `最上位の特権で実行する`（管理者権限）

3. **「トリガー」タブ**
   - `新規` → `タスクの開始: ログにイベントが記録されたとき`
   - `ログ:` **`Application`**
   - `ソース:` **`Application Error`, `Windows Error Reporting`**
   - `イベント ID:` **`1000, 1001, 1002, 7031`**

4. * *「操作」タブ**
   - `新規` → `プログラムの開始`
   - `プログラム/スクリプト` → **`C:\Program Files\MyMonitorApp\MyMonitorApp.exe`**

---

## **3. これで実現できること**
| 機能 | 方法 |
|------|------|
| **クラッシュ監視** | Windows イベント ID **1000, 1001, 1002, 7031** を監視 |
| **アプリ自動再起動** | C# で `Process.Start(AppPath)` を実行 |
| **通知（メール + Slack）** | SMTP 経由のメール送信 & Slack Webhook |
| **再起動回数制限** | 3 回連続クラッシュしたら管理者に警告 |
| **ログ記録** | `C:\logs\myapp-restart.log` に再起動履歴を保存 |

---

## **4. まとめ**
✅ **C# で "クラッシュ検知 + アプリ再起動 + 通知" を完全自動化！**  
✅ **タスクスケジューラをトリガーにし、OS 側で監視 → "プッシュ型" のリカバリー！**  
✅ **クラッシュ回数をカウントし、無限ループを防止！**  
✅ **管理者へ即時通知 & ログ保存で、運用負担ゼロ！**

🚀 **これが "最強のクラッシュ復旧 & 監視システム" の C# 実装！** 🎉