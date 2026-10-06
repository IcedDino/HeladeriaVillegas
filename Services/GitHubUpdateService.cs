using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeladeriaPOS.Services;

public sealed record UpdatePackage(string Version, string DownloadUrl, long Size, string Sha256);

/// <summary>GitHub's public Releases API, independent of MAUI and the image-search client.</summary>
public sealed class GitHubUpdateService(HttpClient client)
{
    public const string InstallerName = "HeladeriaVillegas-Setup-x64.exe";
    public const string Repository = "IcedDino/HeladeriaVillegas";
    public const long MaximumInstallerSize = 2L * 1024 * 1024 * 1024;

    public static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        return value is not null && Regex.IsMatch(value, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)
            && System.Version.TryParse(value, out version!);
    }

    public static bool IsValidPackage(UpdatePackage? package)
    {
        if (package is null || !TryParseVersion(package.Version, out _) || package.Size <= 0
            || package.Size > MaximumInstallerSize || package.Sha256 is null
            || !Regex.IsMatch(package.Sha256, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)) return false;
        if (!Uri.TryCreate(package.DownloadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        return uri.AbsolutePath == $"/{Repository}/releases/download/v{package.Version}/{InstallerName}"
            || uri.AbsolutePath == $"/{Repository}/releases/download/{package.Version}/{InstallerName}";
    }

    public async Task<UpdatePackage?> FindUpdateAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        using var request = CreateRequest($"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var body = new MemoryStream();
        await CopyLimitedAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), body, 2 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (Flag(root, "draft") || Flag(root, "prerelease")) return null;
        string? tag = Text(root, "tag_name");
        string? releaseVersion = tag?.StartsWith('v') == true ? tag[1..] : tag;
        if (!TryParseVersion(releaseVersion, out var version) || version <= currentVersion
            || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (Text(asset, "name") != InstallerName || Text(asset, "state") != "uploaded") continue;
            string? digest = Text(asset, "digest");
            if (digest?.StartsWith("sha256:", StringComparison.Ordinal) != true
                || !asset.TryGetProperty("size", out var size) || !size.TryGetInt64(out long length)) continue;
            var package = new UpdatePackage(releaseVersion!, Text(asset, "browser_download_url") ?? "", length, digest[7..]);
            if (IsValidPackage(package)) return package;
        }
        return null;
    }

    public async Task DownloadAsync(UpdatePackage package, string destination, CancellationToken cancellationToken)
    {
        if (!IsValidPackage(package)) throw new InvalidDataException("Release o instalador no válido.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = destination + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            using var request = CreateRequest(package.DownloadUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long advertised && advertised != package.Size)
                throw new InvalidDataException("El tamaño de la descarga no coincide con el Release.");
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await CopyLimitedAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), output, package.Size, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (!await VerifyAsync(package, temporary, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("La descarga no superó la verificación SHA-256.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<bool> VerifyAsync(UpdatePackage package, string path, CancellationToken cancellationToken)
    {
        if (!IsValidPackage(package) || !File.Exists(path) || new FileInfo(path).Length != package.Size) return false;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        byte[] digest = await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(package.Sha256));
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("HeladeriaVillegas-Update", "1.0"));
        return request;
    }

    private static bool Flag(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long limit, CancellationToken token)
    {
        using (source)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                total += read;
                if (total > limit) throw new InvalidDataException("La descarga excede el tamaño permitido.");
                await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            }
        }
    }
}
