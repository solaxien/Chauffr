using System.Text.Json.Serialization;
using Chauffr.Core.Safety;

namespace Chauffr.Core.Models;

public record ActionPayloadData(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("targetSelector")] string? TargetSelector = null,
    [property: JsonPropertyName("scrollBehavior")] string ScrollBehavior = "smooth",
    [property: JsonPropertyName("interaction")] string Interaction = "pulse",
    [property: JsonPropertyName("openDrawerSelector")] string? OpenDrawerSelector = null,
    [property: JsonPropertyName("tooltipText")] string? TooltipText = null
);

public record ActionSecurityMetadata(
    [property: JsonPropertyName("requiresConfirmation")] bool RequiresConfirmation,
    [property: JsonPropertyName("targetHash")] string? TargetHash,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc
);

public record CoBrowsingAction(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tier")] ActionTier Tier,
    [property: JsonPropertyName("payload")] ActionPayloadData Payload,
    [property: JsonPropertyName("security")] ActionSecurityMetadata Security
);

public record AssistantResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("action")] CoBrowsingAction? Action = null,
    [property: JsonPropertyName("sources")] IReadOnlyList<string>? Sources = null
);
