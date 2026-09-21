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
}
