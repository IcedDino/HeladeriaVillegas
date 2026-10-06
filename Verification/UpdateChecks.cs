using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeladeriaPOS.Services;

internal static class UpdateChecks
{
    public static async Task RunAsync()
    {
        // These tests catch downgrades, untrusted executable URLs and incomplete downloads.
        byte[] bytes = Encoding.UTF8.GetBytes("verified installer fixture");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string url = "https://github.com/IcedDino/HeladeriaVillegas/releases/download/v1.10.0/HeladeriaVillegas-Setup-x64.exe";
        var package = new UpdatePackage("1.10.0", url, bytes.Length, hash);
        var handler = new UpdateHttpHandler(bytes);
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateService(client);
        handler.Json = Release("v1.10.0", url, bytes.Length, "sha256:" + hash);
        Assert((await service.FindUpdateAsync(new Version(1, 9, 0), default))?.Version == "1.10.0", "Numerical version comparison");
        Assert(await service.FindUpdateAsync(new Version(1, 10, 0), default) is null, "No reinstall");
        Assert(await service.FindUpdateAsync(new Version(2, 0, 0), default) is null, "No downgrade");
        foreach (string tag in new[] { "v1.10.0-beta", "latest", "v1.10", "v1.10.0.1" })
        {
            handler.Json = Release(tag, url, bytes.Length, "sha256:" + hash);
            Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Reject invalid tag " + tag);
        }
        foreach (string digest in new[] { "", "sha256:wrong", "md5:" + hash })
        {
            handler.Json = Release("v1.10.0", url, bytes.Length, digest);
            Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Require SHA256");
        }
        foreach (string untrusted in new[] { url.Replace("github.com", "github.com.evil.test"), url.Replace("IcedDino", "Other"), url.Replace("https:", "http:") })
        {
            handler.Json = Release("v1.10.0", untrusted, bytes.Length, "sha256:" + hash);
            Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Reject foreign asset URL");
        }
        handler.Json = Release("v1.10.0", url, bytes.Length, "sha256:" + hash, prerelease: true);
        Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Ignore prerelease");
        handler.Json = Release("v1.10.0", url, bytes.Length, "sha256:" + hash, draft: true);
        Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Ignore draft");
        handler.Json = "{\"tag_name\":\"v1.10.0\",\"assets\":[]}";
        Assert(await service.FindUpdateAsync(new Version(1, 0, 0), default) is null, "Missing installer");
        foreach (HttpStatusCode status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests })
        {
            handler.Status = status;
            await ThrowsAsync<HttpRequestException>(() => service.FindUpdateAsync(new Version(1, 0, 0), default));
        }
        handler.Status = HttpStatusCode.OK;
        handler.Json = "broken json";
        await ThrowsAsync<JsonException>(() => service.FindUpdateAsync(new Version(1, 0, 0), default));

        string directory = Path.Combine(Path.GetTempPath(), "update-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new UpdateStateStore(directory);
            await service.DownloadAsync(package, store.InstallerPath, default);
            Assert(await service.VerifyAsync(package, store.InstallerPath, default), "Downloaded installer verifies");
            store.Write(new PendingUpdate(package, null));
            Assert(store.Read()?.Package == package, "Persist pending across restarts");
            handler.Bytes = Encoding.UTF8.GetBytes("corruption");
            await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(package, store.InstallerPath, default));
            Assert(await service.VerifyAsync(package, store.InstallerPath, default), "Failed download preserves previous valid installer");
            Assert(!Directory.GetFiles(directory, "*.partial*").Any(), "Remove partial download");
            Assert(!await service.VerifyAsync(package with { Size = 99 }, store.InstallerPath, default), "Reject wrong size");
            Assert(!await service.VerifyAsync(package with { Sha256 = new string('0', 64) }, store.InstallerPath, default), "Reject wrong hash");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => service.DownloadAsync(package, store.InstallerPath, cancelled.Token));
            Assert(await service.VerifyAsync(package, store.InstallerPath, default), "Cancellation preserves installer");
            File.WriteAllText(Path.Combine(directory, "pending.json"), "bad json");
            Assert(store.Read() is null, "Corrupt pending state ignored");
            store.Write(new PendingUpdate(package with { DownloadUrl = "https://evil.test/setup.exe" }, null));
            Assert(store.Read() is null, "Manipulated pending rejected");
            store.Clear();
            Assert(store.Read() is null, "Clear pending after success");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("OK: Releases, versiones, URLs, digest, descargas y estado pendiente");
    }

    private static string Release(string tag, string url, long size, string digest, bool prerelease = false, bool draft = false) =>
        JsonSerializer.Serialize(new { tag_name = tag, prerelease, draft, assets = new[] { new { name = "HeladeriaVillegas-Setup-x64.exe", browser_download_url = url, size, digest, state = "uploaded" } } });

    internal static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception("Fallo actualizador: " + name);
    }

    internal static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}

internal sealed class UpdateHttpHandler(byte[] bytes) : HttpMessageHandler
{
    public string Json { get; set; } = "{}";
    public byte[] Bytes { get; set; } = bytes;
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public Exception? Failure { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null) throw Failure;
        if (request.RequestUri?.Host == "api.github.com")
        {
            UpdateChecks.Assert(request.Headers.UserAgent.Any(), "GitHub requires User-Agent");
            UpdateChecks.Assert(request.RequestUri.AbsolutePath == "/repos/IcedDino/HeladeriaVillegas/releases/latest", "Query intended repository");
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Json) });
        }
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new ByteArrayContent(Bytes) });
    }
}
