using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Chauffr.Api.Filters;
using Chauffr.Core.Models;
using Chauffr.Core.Safety;
using Chauffr.Indexing;

namespace Chauffr.Api.Services;

public record ClientTelemetry(
    [property: JsonPropertyName("currentUrl")] string CurrentUrl,
    [property: JsonPropertyName("visibleHeadings")] List<string>? VisibleHeadings = null,
    [property: JsonPropertyName("scrollDepthPercent")] int ScrollDepthPercent = 0
);

public class QwenOrchestrationService
{
    private readonly HttpClient _httpClient;
    private readonly HierarchyBuilder _hierarchy;
    private readonly HybridSearchStore _searchStore;
    private readonly ActionSafetyFilter _safetyFilter;
    private readonly string _ollamaEndpoint;
    private readonly string _modelName;

    public QwenOrchestrationService(
        IConfiguration config,
        HierarchyBuilder hierarchy,
        HybridSearchStore searchStore,
        ActionSafetyFilter safetyFilter,
        HttpClient? httpClient = null)
    {
        _hierarchy = hierarchy;
        _searchStore = searchStore;
        _safetyFilter = safetyFilter;
        _httpClient = httpClient ?? new HttpClient();

        _ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
        _modelName = config["Ollama:Model"] ?? "qwen2.5:3b";
    }

    public async Task<AssistantResponse> ProcessUserMessageAsync(
        string userMessage,
        ClientTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        // 1. Macro Hierarchy Search (Fast path for navigation)
        var macroMatches = _hierarchy.SearchHierarchy(userMessage);

        // 2. Micro Hybrid Search (Content details & RAG)
        var contentMatches = _searchStore.SearchHybrid(userMessage, topK: 3);

        // 3. Assemble prompt with grounded context
        var systemPrompt = BuildSystemPrompt(telemetry, macroMatches, contentMatches);

        try
        {
            // Call local Qwen 2.5 via Ollama API
            var requestBody = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                format = "json",
                stream = false,
                options = new
                {
                    temperature = 0.1,
                    num_ctx = 4096
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_ollamaEndpoint}/api/chat",
                requestBody,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
                var rawMessage = jsonNode?["message"]?["content"]?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(rawMessage))
                {
                    var parsed = ParseAndSecureResponse(rawMessage, macroMatches);
                    if (parsed != null) return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QwenOrchestration] Ollama connection notice: {ex.Message}. Falling back to deterministic planner.");
        }

        // Fallback: Deterministic navigation response if Ollama is unreachable
        return CreateDeterministicFallback(userMessage, macroMatches, contentMatches);
    }

    private string BuildSystemPrompt(
        ClientTelemetry telemetry,
        List<(HierarchyNode Node, double Score, ActionableTarget? Target)> macroMatches,
        List<SearchResultItem> contentMatches)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are Chauffr AI, an intelligent website copilot powered by Qwen 2.5.");
        sb.AppendLine("Your goal is to answer user questions and provide real-time co-browsing UI navigation.");
        sb.AppendLine($"Current User Location: {telemetry.CurrentUrl}");

        if (telemetry.VisibleHeadings != null && telemetry.VisibleHeadings.Count > 0)
        {
            sb.AppendLine($"Currently Visible Sections: {string.Join(", ", telemetry.VisibleHeadings)}");
        }

        sb.AppendLine("\nAvailable Navigation Targets (Macro Index):");
        foreach (var m in macroMatches.Take(3))
        {
            sb.AppendLine($"- Route: {m.Node.Path} | Title: {m.Node.Title}");
            if (m.Target != null)
            {
                sb.AppendLine($"  Target Selector: {m.Target.Selector} ({m.Target.Label})");
            }
        }

        sb.AppendLine("\nRelevant Knowledge Base Chunks (Micro Hybrid RAG):");
        foreach (var c in contentMatches)
        {
            sb.AppendLine($"[{c.Chunk.Heading} - {c.Chunk.Url}]: {c.Chunk.Content}");
        }

        sb.AppendLine("\nOUTPUT RULES:");
        sb.AppendLine("You MUST return ONLY valid JSON matching this exact structure:");
        sb.AppendLine(@"{
  ""message"": ""Conversational response explaining what you are doing or answering the question."",
  ""action"": {
    ""type"": ""NAVIGATE_AND_HIGHLIGHT"",
    ""tier"": 1,
    ""payload"": {
      ""url"": ""/target-page"",
      ""targetSelector"": ""#target-id"",
      ""scrollBehavior"": ""smooth"",
      ""interaction"": ""pulse""
    }
  }
}");
        sb.AppendLine("If no navigation is required, set 'action' to null.");
        sb.AppendLine("NEVER emit selectors that click forms, inputs, submit buttons, or destructive actions.");
        return sb.ToString();
    }

    private AssistantResponse? ParseAndSecureResponse(
        string rawJson,
        List<(HierarchyNode Node, double Score, ActionableTarget? Target)> macroMatches)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string message = root.GetProperty("message").GetString() ?? "Here is the information you requested.";
            CoBrowsingAction? action = null;

            if (root.TryGetProperty("action", out var actionElem) && actionElem.ValueKind == JsonValueKind.Object)
            {
                string type = actionElem.GetProperty("type").GetString() ?? "NAVIGATE_AND_HIGHLIGHT";
                int tierVal = actionElem.TryGetProperty("tier", out var tElem) ? tElem.GetInt32() : 1;
                var tier = (ActionTier)tierVal;

                var payloadElem = actionElem.GetProperty("payload");
                string url = payloadElem.GetProperty("url").GetString() ?? "/";
                string? targetSelector = payloadElem.TryGetProperty("targetSelector", out var ts) ? ts.GetString() : null;
                string scrollBehavior = payloadElem.TryGetProperty("scrollBehavior", out var sb) ? sb.GetString() ?? "smooth" : "smooth";
                string interaction = payloadElem.TryGetProperty("interaction", out var it) ? it.GetString() ?? "pulse" : "pulse";
                string? openDrawer = payloadElem.TryGetProperty("openDrawerSelector", out var od) ? od.GetString() : null;

                var rawAction = new CoBrowsingAction(
                    Type: type,
                    Tier: tier,
                    Payload: new ActionPayloadData(url, targetSelector, scrollBehavior, interaction, openDrawer),
                    Security: new ActionSecurityMetadata(false, null, DateTime.UtcNow)
                );

                // Pass through Action Safety Gateway
                action = _safetyFilter.SanitizeAndValidate(rawAction);
            }

            return new AssistantResponse(message, action);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QwenOrchestration] Error parsing/securing response: {ex.Message}");
            return null;
        }
    }

    private AssistantResponse CreateDeterministicFallback(
        string query,
        List<(HierarchyNode Node, double Score, ActionableTarget? Target)> macroMatches,
        List<SearchResultItem> contentMatches)
    {
        if (macroMatches.Count > 0)
        {
            var best = macroMatches.First();
            string selector = best.Target?.Selector ?? "";

            CoBrowsingAction? action = null;
            if (!string.IsNullOrEmpty(selector) && SelectorValidator.IsSelectorSafe(selector, out _))
            {
                var rawAction = new CoBrowsingAction(
                    Type: "NAVIGATE_AND_HIGHLIGHT",
                    Tier: ActionTier.SafeNavigation,
                    Payload: new ActionPayloadData(best.Node.Path, selector, "smooth", "pulse"),
                    Security: new ActionSecurityMetadata(false, null, DateTime.UtcNow)
                );

                try
                {
                    action = _safetyFilter.SanitizeAndValidate(rawAction);
                }
                catch { /* Ignore if unverified */ }
            }

            return new AssistantResponse(
                Message: $"I found the relevant section: **{best.Node.Title}**.",
                Action: action
            );
        }

        if (contentMatches.Count > 0)
        {
            var top = contentMatches.First();
            return new AssistantResponse(
                Message: $"Based on our knowledge base:\n\n{top.Chunk.Content}",
                Action: null,
                Sources: new[] { top.Chunk.Url }
            );
        }

        return new AssistantResponse(
            Message: "I couldn't locate a specific matching page or documentation for that query. Could you clarify what you're looking for?",
            Action: null
        );
    }
}
