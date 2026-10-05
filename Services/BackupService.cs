using Microsoft.Data.Sqlite;

namespace HeladeriaPOS.Services;

public sealed class BackupService : IDisposable
{
    public static readonly TimeSpan AutomaticBackupInterval = TimeSpan.FromHours(12);
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _automaticTask;
    private readonly string _database = Path.Combine(FileSystem.AppDataDirectory, "pos.db");
    private readonly string _backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "Backups");

    public BackupService(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public string BackupDirectoryPath => _backupDirectory;

    public string? LatestBackupPath
    {
        get
        {
            if (!Directory.Exists(_backupDirectory))
                return null;
            return Directory.GetFiles(_backupDirectory, "pos_*.db").OrderByDescending(path => path).FirstOrDefault();
        }
    }

    public string CreateBackup()
    {
        lock (_gate) return CreateBackupCore();
    }

    private string CreateBackupCore()
    {
        Directory.CreateDirectory(_backupDirectory);
        string path = Path.Combine(_backupDirectory, $"pos_{_clock.GetLocalNow():yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.db");
        string temporary = path + ".tmp";
        try
        {
            using (var source = new SqliteConnection($"Data Source={_database};Mode=ReadOnly;Pooling=False"))
            using (var destination = new SqliteConnection($"Data Source={temporary};Pooling=False"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
                using var check = destination.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La copia no superó la verificación de integridad.");
            }
            File.SetLastWriteTimeUtc(temporary, _clock.GetUtcNow().UtcDateTime);
            File.Move(temporary, path);
            return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string? BackupIfDue()
    {
        lock (_gate)
        {
            DateTime? latest = Directory.Exists(_backupDirectory)
                ? Directory.EnumerateFiles(_backupDirectory, "pos_*.db").Select(File.GetLastWriteTimeUtc)
                    .Select(time => (DateTime?)time).Max()
                : null;
            if (latest is not null && _clock.GetUtcNow().UtcDateTime - latest.Value < AutomaticBackupInterval)
                return null;
            return CreateBackupCore();
        }
    }

    public void StartAutomaticBackups(Action<Exception> onError)
    {
        lock (_gate)
        {
            if (_automaticTask is not null) return;
            _automaticTask = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                try
                {
                    do
                    {
                        try { BackupIfDue(); }
                        catch (Exception exception) { onError(exception); }
                    } while (await timer.WaitForNextTickAsync(_stop.Token));
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            });
        }
    }

    public void Dispose() => _stop.Cancel();

    public void ScheduleRestore(string backupPath)
    {
        string pending = Path.Combine(FileSystem.AppDataDirectory, "restore_pending.db");
        if (File.Exists(pending)) File.Delete(pending);
        using var source = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
        using var destination = new SqliteConnection($"Data Source={pending}");
        source.Open();
        using (var check = source.CreateCommand())
        {
            check.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El archivo no es una base de datos íntegra.");
        }
        destination.Open();
        source.BackupDatabase(destination);
    }

    public static void ApplyPendingRestore(string directory)
    {
        string pending = Path.Combine(directory, "restore_pending.db");
        if (!File.Exists(pending)) return;
        string previous = Path.Combine(directory, "BeforeRestore", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(previous);
        var moved = new List<string>();
        try
        {
            foreach (string name in new[] { "pos.db", "pos.db-wal", "pos.db-shm" })
            {
                string original = Path.Combine(directory, name);
                if (!File.Exists(original)) continue;
                File.Move(original, Path.Combine(previous, name));
                moved.Add(name);
            }
            File.Move(pending, Path.Combine(directory, "pos.db"));
        }
        catch
        {
            foreach (string name in moved)
            {
                string original = Path.Combine(directory, name);
                if (!File.Exists(original)) File.Move(Path.Combine(previous, name), original);
            }
            throw;
        }
    }
}
