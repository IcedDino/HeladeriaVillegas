using System.Text.Json;

namespace HeladeriaPOS.Services;

public sealed record PendingUpdate(UpdatePackage Package, DateTimeOffset? LastAttemptUtc);

public sealed class UpdateStateStore(string directory)
{
    public string InstallerPath { get; } = Path.Combine(Path.GetFullPath(directory), GitHubUpdateService.InstallerName);
    private readonly string _statePath = Path.Combine(Path.GetFullPath(directory), "pending.json");

    public PendingUpdate? Read()
    {
        try
        {
            if (!File.Exists(_statePath) || new FileInfo(_statePath).Length > 16384) return null;
            var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(_statePath));
            return pending is not null && GitHubUpdateService.IsValidPackage(pending.Package) ? pending : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public void Write(PendingUpdate state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        string temporary = _statePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, _statePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Clear() => File.Delete(_statePath);
}
