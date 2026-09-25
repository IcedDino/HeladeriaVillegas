using Microsoft.Data.Sqlite;

namespace HeladeriaPOS.Services;

public sealed class BackupService
{
    private readonly string _database = Path.Combine(FileSystem.AppDataDirectory, "pos.db");
    private readonly string _backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "Backups");

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

    public bool HasTodayBackup
    {
        get
        {
            if (!Directory.Exists(_backupDirectory))
                return false;
            return Directory.GetFiles(_backupDirectory, $"pos_{DateTime.Now:yyyyMMdd}_*.db").Length > 0;
        }
    }

    public string CreateBackup()
    {
        Directory.CreateDirectory(_backupDirectory);
        string path = Path.Combine(_backupDirectory, $"pos_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        using var source = new SqliteConnection($"Data Source={_database};Mode=ReadOnly");
        using var destination = new SqliteConnection($"Data Source={path}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
        using var check = destination.CreateCommand();
        check.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La copia no superó la verificación de integridad.");
        return path;
    }

    public void BackupOncePerDay()
    {
        Directory.CreateDirectory(_backupDirectory);
        if (Directory.GetFiles(_backupDirectory, $"pos_{DateTime.Now:yyyyMMdd}_*.db").Length == 0)
            CreateBackup();
    }

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
