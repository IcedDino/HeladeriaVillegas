using HeladeriaPOS.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

internal static class DataPreservationChecks
{
    public static async Task RunAsync(string source)
    {
        string target = Path.Combine(Path.GetTempPath(), "heladeria-preservation-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(source), Mode = SqliteOpenMode.ReadOnly }.ToString()))
            using (var copy = new SqliteConnection($"Data Source={target}"))
            {
                original.Open(); copy.Open(); original.BackupDatabase(copy);
            }
            var before = Snapshot(target);
            var factory = new PreservationFactory(new DbContextOptionsBuilder<PosDbContext>().UseSqlite($"Data Source={target}").Options);
            await new DatabaseInitializer(factory).InitializeAsync();
            await new DatabaseInitializer(factory).InitializeAsync();
            using var connection = new SqliteConnection($"Data Source={target}");
            connection.Open();
            VerifySnapshot(connection, before);
            Console.WriteLine("OK: real database COPY migration and integrity; source never modified");
        }
        finally { SqliteConnection.ClearAllPools(); foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(target + suffix)) File.Delete(target + suffix); }
    }
    public static void Compare(string beforePath, string afterPath)
    {
        var before = Snapshot(beforePath);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(afterPath), Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        VerifySnapshot(connection, before);
        Console.WriteLine("OK: installed database retains every original row and field; integrity_check=ok");
    }
    private static void VerifySnapshot(SqliteConnection connection, Dictionary<string, (string[] Columns, List<string> Rows)> before)
    {
            foreach (var table in before)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT {string.Join(",", table.Value.Columns.Select(Quote))} FROM {Quote(table.Key)} ORDER BY Id";
                using var reader = command.ExecuteReader();
                var after = new HashSet<string>();
                while (reader.Read()) after.Add(Row(reader));
                if (table.Value.Rows.Any(row => !after.Contains(row))) throw new Exception("Existing data was modified: " + table.Key);
                Console.WriteLine($"OK: {table.Key}: {table.Value.Rows.Count} original rows preserved, all original fields unchanged");
            }
            using var integrity = connection.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check";
            if ((string?)integrity.ExecuteScalar() != "ok") throw new Exception("Database integrity check failed");
    }
    private static Dictionary<string, (string[] Columns, List<string> Rows)> Snapshot(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly }.ToString()); connection.Open();
        using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var names = new List<string>(); using (var reader = tables.ExecuteReader()) while (reader.Read()) names.Add(reader.GetString(0));
        var result = new Dictionary<string, (string[], List<string>)>();
        foreach (string name in names)
        {
            using var command = connection.CreateCommand(); command.CommandText = $"SELECT * FROM {Quote(name)} ORDER BY Id";
            using var reader = command.ExecuteReader();
            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var rows = new List<string>(); while (reader.Read()) rows.Add(Row(reader));
            result[name] = (columns, rows);
        }
        return result;
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Row(SqliteDataReader reader) => JsonSerializer.Serialize(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
    private sealed class PreservationFactory(DbContextOptions<PosDbContext> options) : IDbContextFactory<PosDbContext>
    { public PosDbContext CreateDbContext() => new(options); }
}
