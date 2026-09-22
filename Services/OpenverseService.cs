using System.Net.Http.Json;
using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public sealed class OpenverseService
{
    private readonly HttpClient _httpClient;

    public OpenverseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<OpenverseImageResult>> SearchImagesAsync(
        string query,
        int pageSize = 16,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        int safePageSize = Math.Clamp(pageSize, 1, 20);
        string request = $"v1/images/?q={Uri.EscapeDataString(query.Trim())}&page_size={safePageSize}&license_type=commercial&mature=false";
        OpenverseSearchResponse? response = await _httpClient.GetFromJsonAsync<OpenverseSearchResponse>(request, cancellationToken);

        return response?.Results
            .Where(image => !string.IsNullOrWhiteSpace(image.Thumbnail))
            .ToList() ?? [];
    }

    public async Task<string> CacheImageAsync(
        OpenverseImageResult image,
        CancellationToken cancellationToken = default)
    {
        if (image.IsLocalLibraryImage || !Uri.TryCreate(image.Thumbnail, UriKind.Absolute, out Uri? imageUri))
            return image.Thumbnail;

        using HttpResponseMessage response = await _httpClient.GetAsync(imageUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        string extension = response.Content.Headers.ContentType?.MediaType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        string directory = Path.Combine(FileSystem.AppDataDirectory, "ProductImages");
        Directory.CreateDirectory(directory);
        string safeId = new string(image.Id.Where(char.IsLetterOrDigit).Take(32).ToArray());
        if (safeId.Length == 0)
            safeId = Guid.NewGuid().ToString("N");

        string localPath = Path.Combine(directory, $"product_{safeId}{extension}");
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = File.Create(localPath);
        await input.CopyToAsync(output, cancellationToken);
        return localPath;
    }
}
