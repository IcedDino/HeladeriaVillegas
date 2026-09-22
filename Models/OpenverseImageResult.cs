using System.Text.Json.Serialization;

namespace HeladeriaPOS.Models;

public sealed class OpenverseImageResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;

    [JsonPropertyName("creator")]
    public string? Creator { get; set; }

    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    [JsonPropertyName("license_version")]
    public string? LicenseVersion { get; set; }

    [JsonPropertyName("license_url")]
    public string? LicenseUrl { get; set; }

    [JsonPropertyName("foreign_landing_url")]
    public string? SourceUrl { get; set; }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Imagen sin título" : Title;

    [JsonIgnore]
    public string AttributionSummary => $"{(string.IsNullOrWhiteSpace(Creator) ? "Autor desconocido" : Creator)} · {License.ToUpperInvariant()} {LicenseVersion}".Trim();

    [JsonIgnore]
    public string? LibraryCategory { get; set; }

    [JsonIgnore]
    public string SearchKeywords { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsLocalLibraryImage => !string.IsNullOrWhiteSpace(LibraryCategory);
}

internal sealed class OpenverseSearchResponse
{
    [JsonPropertyName("results")]
    public List<OpenverseImageResult> Results { get; set; } = [];
}
