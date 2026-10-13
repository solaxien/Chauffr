using System.Text.Json.Serialization;

namespace Chauffr.Core.Models;

/// <summary>
/// Represents an interactive anchor or UI target on a specific route.
/// </summary>
public record ActionableTarget(
    [property: JsonPropertyName("selector")] string Selector,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("targetType")] string TargetType = "anchor" // "anchor", "drawer", "modal-trigger"
);

/// <summary>
/// Represents a node in the website's hierarchical navigation tree.
/// </summary>
public class HierarchyNode
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("breadcrumbs")]
    public List<string> Breadcrumbs { get; set; } = [];

    [JsonPropertyName("actionableTargets")]
    public List<ActionableTarget> ActionableTargets { get; set; } = [];

    [JsonPropertyName("children")]
    public List<HierarchyNode> Children { get; set; } = [];

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }
}
