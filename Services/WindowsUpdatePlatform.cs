#if WINDOWS
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HeladeriaPOS.Services;

/// <summary>Installation-specific Windows operations. The lifetime mutex also excludes the updater.</summary>
public sealed class WindowsUpdatePlatform : IDisposable
{
    private readonly string _executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable unavailable.");
    private readonly string _directory;
    private readonly string _updates = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HeladeriaVillegasUpdates");
    private readonly Mutex _mutex;
    private bool _ownsMutex;
    private readonly string _mutexName;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public WindowsUpdatePlatform()
    {
        _directory = Path.GetDirectoryName(_executable)!;
        _mutexName = "Local\\HeladeriaVillegas-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_directory.ToUpperInvariant())));
        _mutex = new Mutex(false, _mutexName);
    }

    public bool IsInstalled()
    {
#if DEBUG
        return false;
#else
        return File.Exists(Path.Combine(_directory, "unins000.exe"))
            && File.Exists(Path.Combine(_directory, "apply-update.ps1"));
#endif
    }

    // Acquire/release on the MAUI UI thread. A second launch must not enter caja during replacement.
    public bool TryAcquireInstance()
    {
        if (!IsInstalled() || _ownsMutex) return true;
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        return _ownsMutex;
    }

    public WindowsUpdateCoordinator CreateCoordinator(BackupService backup)
    {
        bool storageReady = false;
        if (IsInstalled())
        {
            try { WindowsUpdateStorage.EnsureSecure(_updates); storageReady = true; }
            catch (Exception exception) { StartupLog.Write("Updates disabled: protected storage unavailable: " + exception); }
        }
        var version = typeof(WindowsUpdatePlatform).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return new WindowsUpdateCoordinator(new GitHubUpdateService(_client), new UpdateStateStore(_updates), version,
            () => Connectivity.NetworkAccess == NetworkAccess.Internet, () => IsInstalled() && storageReady, OtherInstance,
            () => { if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, "pos.db"))) backup.CreateBackup(); },
            Launch, exception => StartupLog.Write("Update: " + exception));
    }

    private bool OtherInstance()
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_executable)))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                try
                {
                    if (string.Equals(process.MainModule?.FileName, _executable, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { /* Process already exited. */ }
                catch (System.ComponentModel.Win32Exception) { return true; }
            }
        }
        return false;
    }

    private void Launch(PendingUpdate pending)
    {
        Directory.CreateDirectory(_updates);
        string script = Path.Combine(_updates, "apply-update.ps1");
        File.Copy(Path.Combine(_directory, "apply-update.ps1"), script, true);
        string configuration = Path.Combine(_updates, "apply-update.json");
        string readyName = "Local\\HeladeriaVillegasUpdateReady-" + Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, readyName);
        using var current = Process.GetCurrentProcess();
        File.WriteAllText(configuration, JsonSerializer.Serialize(new
        {
            OriginalPid = Environment.ProcessId,
            OriginalStartUtc = current.StartTime.ToUniversalTime().ToString("O"),
            ExecutablePath = _executable, MutexName = _mutexName,
            Package = pending.Package, ReadyEventName = readyName
        }));
        // EncodedCommand avoids shell parsing of spaces, apostrophes or dollar signs in user paths.
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
        string command = "& " + Quote(script) + " -ConfigurationPath " + Quote(configuration);
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        using var auxiliary = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar el actualizador.");
        var deadline = Stopwatch.StartNew();
        while (!ready.WaitOne(100))
        {
            if (auxiliary.HasExited) throw new InvalidOperationException("El actualizador terminó antes de estar listo.");
            if (deadline.Elapsed >= TimeSpan.FromSeconds(15))
            {
                // Terminate only our own waiting helper so it cannot install after we decide to keep caja open.
                try { if (!auxiliary.HasExited) auxiliary.Kill(); }
                catch (InvalidOperationException) { }
                throw new TimeoutException("El actualizador no confirmó que estuviera listo.");
            }
        }
        StartupLog.Write("Update helper launched pid=" + auxiliary.Id);
    }

    public void Dispose()
    {
        if (_ownsMutex) { _mutex.ReleaseMutex(); _ownsMutex = false; }
        _mutex.Dispose();
        // Downloads may still be cancelling; their owned HttpClient is retained until process exit.
    }
}
#endif
