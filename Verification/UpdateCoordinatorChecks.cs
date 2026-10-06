using HeladeriaPOS.Services;
using System.Security.Cryptography;
using System.Text;

internal static class UpdateCoordinatorChecks
{
    public static async Task RunAsync()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("coordinator fixture");
        var package = new UpdatePackage("1.1.0", "https://github.com/IcedDino/HeladeriaVillegas/releases/download/v1.1.0/HeladeriaVillegas-Setup-x64.exe",
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        var handler = new UpdateHttpHandler(bytes)
        {
            Json = System.Text.Json.JsonSerializer.Serialize(new { tag_name = "v1.1.0", draft = false, prerelease = false,
                assets = new[] { new { name = "HeladeriaVillegas-Setup-x64.exe", state = "uploaded", size = bytes.Length, digest = "sha256:" + package.Sha256, browser_download_url = package.DownloadUrl } } })
        };
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateService(client);
        string directory = Path.Combine(Path.GetTempPath(), "coordinator-checks-" + Guid.NewGuid().ToString("N"));
        var store = new UpdateStateStore(directory);
        var clock = new VerificationTimeProvider(DateTimeOffset.Parse("2026-10-05T12:00:00Z"));
        bool online = false, installed = true, otherInstance = false, backupFails = false, launchFails = false;
        int backups = 0, launches = 0;
        using var coordinator = new WindowsUpdateCoordinator(service, store, new Version(1, 0, 0),
            () => online, () => installed, () => otherInstance,
            () => { if (backupFails) throw new IOException("backup failed"); backups++; },
            pending => { if (launchFails) throw new IOException("launch failed"); launches++; },
            _ => { }, clock);
        try
        {
            await coordinator.CheckAsync();
            UpdateChecks.Assert(store.Read() is null && !Directory.Exists(directory), "Offline check does not download");
            online = true; installed = false;
            await coordinator.CheckAsync();
            UpdateChecks.Assert(store.Read() is null, "Development never downloads");
            installed = true;
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => coordinator.CheckAsync()));
            UpdateChecks.Assert(store.Read()?.Package.Version == "1.1.0", "Concurrent checks produce one verified pending update");
            UpdateChecks.Assert(launches == 0, "Never install during check or sale");
            otherInstance = true;
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default) && backups == 0, "Other instance defers installation");
            otherInstance = false; backupFails = true;
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default) && launches == 0, "Backup failure leaves app usable");
            backupFails = false;
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default), "Failed backup respects retry interval");
            clock.UtcNow = clock.UtcNow.AddHours(1);
            launchFails = true;
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default), "Auxiliary launch failure allows opening app");
            launchFails = false;
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default), "Failed launch is not repeated immediately");
            clock.UtcNow = clock.UtcNow.AddHours(1);
            UpdateChecks.Assert(await coordinator.TryInstallPendingAsync(default) && backups == 2 && launches == 1, "Verified pending installs after backup");
            UpdateChecks.Assert(store.Read()?.LastAttemptUtc == clock.UtcNow, "Record attempt before shutting down");

            using var updated = new WindowsUpdateCoordinator(service, store, new Version(1, 1, 0), () => true, () => true,
                () => false, () => throw new Exception("unexpected backup"), _ => throw new Exception("unexpected installer"), _ => { }, clock);
            UpdateChecks.Assert(!await updated.TryInstallPendingAsync(default) && store.Read() is null, "Installed version clears stale pending");
            store.Write(new PendingUpdate(package, null));
            File.WriteAllText(store.InstallerPath, "corrupt");
            UpdateChecks.Assert(!await coordinator.TryInstallPendingAsync(default) && store.Read() is null, "Tampered executable never installs");
            handler.Status = System.Net.HttpStatusCode.TooManyRequests;
            clock.UtcNow = clock.UtcNow.AddHours(1);
            await coordinator.CheckAsync();
            handler.Status = System.Net.HttpStatusCode.OK;
            await coordinator.CheckAsync();
            UpdateChecks.Assert(store.Read() is null, "HTTP failure backs off an hour");
            clock.UtcNow = clock.UtcNow.AddHours(1);
            await coordinator.CheckAsync();
            UpdateChecks.Assert(store.Read() is not null, "HTTP failure eventually retries");
            store.Clear();
            handler.Failure = new TaskCanceledException("timeout");
            clock.UtcNow = clock.UtcNow.AddHours(1);
            await coordinator.CheckAsync();
            UpdateChecks.Assert(store.Read() is null, "Timeout does not stop app");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("OK: coordinación, conexión, respaldo, instalación aplazada y reintentos");
    }
}
