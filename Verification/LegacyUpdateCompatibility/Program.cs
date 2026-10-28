using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeladeriaPOS.Services;

if (args.Length < 3) throw new ArgumentException("Installer, version and test directory are required.");
string installer = Path.GetFullPath(args[0]);
string version = args[1];
string directory = Path.GetFullPath(args[2]);
bool published = args.Contains("--published");
string hash;
await using (var input = File.OpenRead(installer))
    hash = Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();
long size = new FileInfo(installer).Length;
var expected = new UpdatePackage(version,
    $"https://github.com/{GitHubUpdateService.Repository}/releases/download/v{version}/{GitHubUpdateService.InstallerName}", size, hash);
var originalVersion = new Version(1, 0, 0, 0);
var installedVersion = new Version(version + ".0");
if (installedVersion <= originalVersion) throw new Exception("Update must be newer than the original assembly version.");

using var transport = new PackageTransport(installer, expected);
using var client = published ? new HttpClient() : new HttpClient(transport);
client.Timeout = TimeSpan.FromMinutes(10);
var service = new GitHubUpdateService(client);
var package = await service.FindUpdateAsync(originalVersion, default);
Assert(package == expected, "Original 1.0.0 finds expected version, public URL, size and SHA-256");
Assert(await service.FindUpdateAsync(installedVersion, default) is null, "Updated version never reinstalls itself");

if (!published)
{
    transport.Draft = true;
    Assert(await service.FindUpdateAsync(originalVersion, default) is null, "Draft never reaches old installations");
    transport.Draft = false;
    transport.Prerelease = true;
    Assert(await service.FindUpdateAsync(originalVersion, default) is null, "Prerelease is ignored");
    transport.Prerelease = false;
    transport.MissingDigest = true;
    Assert(await service.FindUpdateAsync(originalVersion, default) is null, "GitHub digest is mandatory");
    transport.MissingDigest = false;
}

Directory.CreateDirectory(directory);
var store = new UpdateStateStore(directory);
var actions = new List<string>();
using (var running = Coordinator(originalVersion))
{
    await running.CheckAsync();
    Assert(store.Read()?.Package == expected, "Original updater downloads the actual installer and persists pending state");
    Assert(await service.VerifyAsync(expected, store.InstallerPath, default), "Downloaded EXE matches real package bytes");
    Assert(actions.Count == 0, "Download never starts installation during a sale");
}
using (var restarted = Coordinator(originalVersion))
{
    Assert(await restarted.TryInstallPendingAsync(default), "Next startup accepts pending update");
    Assert(actions.SequenceEqual(new[] { "backup", "launch" }), "Backup precedes handing installer to helper");
}
using (var updated = Coordinator(installedVersion))
{
    Assert(!await updated.TryInstallPendingAsync(default) && store.Read() is null, "New assembly version clears stale pending state");
}
await using (var modified = new FileStream(store.InstallerPath, FileMode.Append, FileAccess.Write))
    await modified.WriteAsync(new byte[] { 0 });
Assert(!await service.VerifyAsync(expected, store.InstallerPath, default), "Modified installer is rejected");
File.Delete(store.InstallerPath);
Console.WriteLine(published ? "PASS: original updater against PUBLIC Release" : "PASS: original updater with simulated GitHub transport and REAL installer");
Console.WriteLine("Installer process launch is simulated; no installed POS or user database was replaced.");

WindowsUpdateCoordinator Coordinator(Version current) => new(service, store, current,
    () => true, () => true, () => false, () => actions.Add("backup"),
    _ => actions.Add("launch"), exception => throw new Exception("Original updater failed", exception));

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("OK: " + message);
}

sealed class PackageTransport(string installer, UpdatePackage package) : HttpMessageHandler
{
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public bool MissingDigest { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContent content;
        if (request.RequestUri!.AbsoluteUri == $"https://api.github.com/repos/{GitHubUpdateService.Repository}/releases/latest")
        {
            content = new StringContent(JsonSerializer.Serialize(new
            {
                tag_name = "v" + package.Version, draft = Draft, prerelease = Prerelease,
                assets = new[] { new { name = GitHubUpdateService.InstallerName, state = "uploaded",
                    size = package.Size, digest = MissingDigest ? null : "sha256:" + package.Sha256,
                    browser_download_url = package.DownloadUrl } }
            }), Encoding.UTF8, "application/json");
        }
        else if (request.RequestUri.AbsoluteUri == package.DownloadUrl)
            content = new StreamContent(File.OpenRead(installer));
        else throw new Exception("Unexpected updater URL: " + request.RequestUri);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
