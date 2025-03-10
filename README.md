using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MyBackgroundService : BackgroundService
{
    private readonly ILogger<MyBackgroundService> _logger;

    public MyBackgroundService(ILogger<MyBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting external process...");
                await RunExternalProcessAsync(@"C:\path\to\yourapp.exe", "", stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing process.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);  // 1分ごとに実行
        }
    }

    private async Task RunExternalProcessAsync(string filePath, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, args) => _logger.LogInformation($"Output: {args.Data}");
            process.ErrorDataReceived += (sender, args) => _logger.LogError($"Error: {args.Data}");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
        }
    }
}




`Process.ExitCode` を取得し、異常終了時に適切な例外を投げるように修正しました。  
- **正常終了 (`ExitCode == 0`) の場合はそのまま完了。**
- **異常終了 (`ExitCode != 0`) の場合は `ProcessExecutionException` を投げる。**
- **キャンセル時 (`CancellationToken` トリガー) はプロセスを強制終了。**

---

### **修正後のコード**
```csharp
private async Task RunExternalProcessAsync(string filePath, string arguments, CancellationToken cancellationToken)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = filePath,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = new Process { StartInfo = startInfo };

    var outputTcs = new TaskCompletionSource<bool>();
    var errorTcs = new TaskCompletionSource<bool>();

    process.OutputDataReceived += (sender, args) =>
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
            _logger.LogInformation($"Output: {args.Data}");
    };

    process.ErrorDataReceived += (sender, args) =>
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
            _logger.LogError($"Error: {args.Data}");
    };

    if (!process.Start())
    {
        throw new InvalidOperationException($"Failed to start process: {filePath}");
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    var exitTask = process.WaitForExitAsync(cancellationToken);
    var completedTask = await Task.WhenAny(exitTask, Task.Delay(Timeout.Infinite, cancellationToken));

    if (completedTask != exitTask)
    {
        process.Kill(true);
        throw new OperationCanceledException("Process execution was canceled.");
    }

    await exitTask; // Ensure process has exited before checking ExitCode

    if (process.ExitCode != 0)
    {
        throw new ProcessExecutionException($"Process exited with code {process.ExitCode}: {filePath} {arguments}", process.ExitCode);
    }
}
```

---

### **追加点**
1. **`Process.ExitCode` を確認し、異常終了時に例外をスロー**
   - `if (process.ExitCode != 0) throw new ProcessExecutionException(...);`
   - **カスタム例外 `ProcessExecutionException` を作成し、`ExitCode` を含める**

2. **キャンセル時にプロセスを強制終了**
   - `Task.WhenAny(exitTask, Task.Delay(Timeout.Infinite, cancellationToken))` でキャンセルを検知。
   - `process.Kill(true);` でプロセスを確実に終了。

3. **カスタム例外 `ProcessExecutionException` を作成**
   - `ExitCode` を保持できるようにし、エラー処理を簡素化。

---

### **カスタム例外 `ProcessExecutionException`**
```csharp
public class ProcessExecutionException : Exception
{
    public int ExitCode { get; }

    public ProcessExecutionException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }
}
```

---

### **改善点まとめ**
✅ **`ExitCode` を取得し、異常終了時に例外を投げる**  
✅ **キャンセル時にプロセスを安全に強制終了する**  
✅ **カスタム例外で `ExitCode` を保持し、エラーハンドリングを簡素化**  

これでより堅牢でモダンな `RunExternalProcessAsync` が完成しました！ 🚀