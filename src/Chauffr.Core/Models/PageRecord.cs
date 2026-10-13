using System.Text.Json.Serialization;

namespace Chauffr.Core.Models;

public record ContentChunk(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("breadcrumbTrail")] string BreadcrumbTrail,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("denseVector")] ReadOnlyMemory<float>? DenseVector = null,
    [property: JsonPropertyName("tokenCount")] int TokenCount = 0
);

public class PageRecord
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    [JsonPropertyName("cleanMarkdown")]
    public string CleanMarkdown { get; set; } = string.Empty;

    [JsonPropertyName("breadcrumbs")]
    public List<string> Breadcrumbs { get; set; } = [];

    [JsonPropertyName("actionableTargets")]
    public List<ActionableTarget> ActionableTargets { get; set; } = [];

    [JsonPropertyName("chunks")]
    public List<ContentChunk> Chunks { get; set; } = [];
}
