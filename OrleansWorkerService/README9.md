### **🔥 `SqlConnection` を DI (`Transient`) にすると `BeginTransaction` が難しい？**
✅ その通り！  
- `SqlConnection` を **`Transient`** にすると、**リクエストごとに新しい接続が作られる** ため、  
  `BeginTransaction()` を **リポジトリの複数のメソッド間で共有するのが難しくなる**。
- `Singleton` で `SqlConnection` を管理すると **接続を共有できるが、競合や `MultipleActiveResultSets` の問題が発生する**。

---

## **✅ 解決策**
### **1️⃣ `IUnitOfWork` パターンを導入する**
`IUnitOfWork` を導入すれば、**トランザクションを明示的に管理しながら、DI で `SqlConnection` を適切に扱える！**  
- `BeginTransaction()` を一度だけ呼び、  
- その `IDbTransaction` をリポジトリ内で共有する。

---

## **🔥 `IUnitOfWork` パターンの実装**
### **🔹 1️⃣ `IUnitOfWork` インターフェース**
```csharp
using System;
using System.Data;
using System.Threading.Tasks;

public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction Transaction { get; }
    
    void BeginTransaction();
    void Commit();
    void Rollback();
}
```

---

### **🔹 2️⃣ `UnitOfWork` クラス**
```csharp
using System.Data;
using System.Data.SqlClient;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        _connection.Open();
    }

    public IDbConnection Connection => _connection;
    public IDbTransaction Transaction => _transaction;

    public void BeginTransaction()
    {
        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection.Dispose();
    }
}
```
✅ `UnitOfWork` を使うことで、**`SqlConnection` を `Transient` にしつつ、トランザクションを管理できる！**

---

### **🔹 3️⃣ `IJobRepository` に `UnitOfWork` を適用**
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IJobRepository
{
    Task<List<ScheduledJob>> FetchScheduledJobsAsync();
    Task UpdateLastRunTimeAsync(int jobId);
}
```

---

### **🔹 4️⃣ `JobRepository` の実装**
```csharp
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

public class JobRepository : IJobRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public JobRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ScheduledJob>> FetchScheduledJobsAsync()
    {
        var jobs = new List<ScheduledJob>();

        using var command = _unitOfWork.Connection.CreateCommand();
        command.CommandText = "SELECT Id, JobName, CronExpression, LastRunTime FROM ScheduledJobs WHERE IsActive = 1";
        command.Transaction = _unitOfWork.Transaction;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            jobs.Add(new ScheduledJob
            {
                Id = reader.GetInt32(0),
                JobName = reader.GetString(1),
                CronExpression = reader.GetString(2),
                LastRunTime = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3)
            });
        }

        return jobs;
    }

    public async Task UpdateLastRunTimeAsync(int jobId)
    {
        using var command = _unitOfWork.Connection.CreateCommand();
        command.CommandText = "UPDATE ScheduledJobs SET LastRunTime = @LastRunTime WHERE Id = @Id";
        command.Parameters.Add(new SqlParameter("@LastRunTime", DateTime.UtcNow));
        command.Parameters.Add(new SqlParameter("@Id", jobId));
        command.Transaction = _unitOfWork.Transaction;

        await command.ExecuteNonQueryAsync();
    }
}
```

---

### **🔹 5️⃣ `JobScheduler` で `UnitOfWork` を使う**
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class JobScheduler : BackgroundService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobRepository _jobRepository;
    private readonly int _maxParallelism = 4;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    public JobScheduler(IUnitOfWork unitOfWork, IJobRepository jobRepository)
    {
        _unitOfWork = unitOfWork;
        _jobRepository = jobRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _unitOfWork.BeginTransaction();
            try
            {
                var jobs = await _jobRepository.FetchScheduledJobsAsync();

                await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism }, async (job, token) =>
                {
                    await job.Execute();
                    await _jobRepository.UpdateLastRunTimeAsync(job.Id);
                });

                _unitOfWork.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
                _unitOfWork.Rollback();
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }
}
```

---

### **🔹 6️⃣ `Program.cs`（最適な DI 設定）**
```csharp
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    static async Task Main()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IUnitOfWork>(sp =>
                    new UnitOfWork("Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;"));

                services.AddSingleton<IJobRepository, JobRepository>();
                services.AddSingleton<JobScheduler>();
                services.AddHostedService(provider => provider.GetRequiredService<JobScheduler>());
            })
            .Build();

        await host.RunAsync();
    }
}
```

---

## **🔥 `IUnitOfWork` の導入で何が良くなる？**
| **課題** | **解決策** |
|------------|--------------------------------------------|
| `Singleton SqlConnection` で競合が発生 | **`UnitOfWork` で `Singleton` に管理** |
| `Transient SqlConnection` だと `BeginTransaction` ができない | **`UnitOfWork` で 1回の処理内でトランザクション管理** |
| `MultipleActiveResultSets` の問題発生 | **1つの `SqlConnection` をトランザクション内で管理** |
| `Rollback()` できない | **`UnitOfWork.Rollback()` を使って安全に戻せる** |

---

## **🚀 結論**
☑ **`SqlConnection` は `Transient` にせず、`UnitOfWork` を `Singleton` にするのがベスト！**  
☑ **トランザクション (`BeginTransaction`, `Commit`, `Rollback`) を適切に管理できる！**  
☑ **複数のリポジトリをまたぐ処理でもトランザクションを維持できる！**

🔥 **本番環境で「速くて安全なスケジューラー」を作るなら `IUnitOfWork` パターンが最適！** 🚀