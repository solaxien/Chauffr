using Chauffr.Api.Filters;
using Chauffr.Api.Hubs;
using Chauffr.Api.Services;
using Chauffr.Core.Models;
using Chauffr.Indexing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// Core indexing and AI services
builder.Services.AddSingleton<HierarchyBuilder>();
builder.Services.AddSingleton<IActionRegistry>(sp => sp.GetRequiredService<HierarchyBuilder>());
builder.Services.AddSingleton<HybridSearchStore>();
builder.Services.AddSingleton<ActionSafetyFilter>();
builder.Services.AddSingleton<QwenOrchestrationService>();

// Configure CORS for website embedding
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigin", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowAnyOrigin");

// Seed initial mock data for immediate testing
var hierarchy = app.Services.GetRequiredService<HierarchyBuilder>();
var searchStore = app.Services.GetRequiredService<HybridSearchStore>();

SeedMockData(hierarchy, searchStore);

// Minimal API Endpoints
app.MapGet("/", () => Results.Ok(new
{
    name = "Chauffr AI API",
    status = "Online",
    head = "Qwen 2.5 (Local Ollama)",
    signalRHub = "/chathub"
}));

app.MapGet("/api/routes", (HierarchyBuilder hb) => Results.Ok(hb.GetAllNodes()));

app.MapPost("/api/message", async (MessageRequest req, QwenOrchestrationService orchestration, CancellationToken ct) =>
{
    var telemetry = req.Telemetry ?? new ClientTelemetry(req.CurrentUrl ?? "/");
    var response = await orchestration.ProcessUserMessageAsync(req.Message, telemetry, ct);
    return Results.Ok(response);
});

app.MapHub<ChatHub>("/chathub");

app.Run();

static void SeedMockData(HierarchyBuilder hierarchy, HybridSearchStore searchStore)
{
    // Seed Sample Macro Routes
    hierarchy.RegisterPage(new PageRecord
    {
        Url = "/pricing",
        Title = "Pricing & Enterprise Plans",
        Breadcrumbs = new List<string> { "Home", "Pricing" },
        ActionableTargets = new List<ActionableTarget>
        {
            new("#enterprise-sla", "Enterprise SLA & Uptime Guarantee", "anchor"),
            new("#faq-billing", "Billing & Payment Methods FAQ", "drawer"),
            new("#starter-tier", "Starter Tier Sign Up", "anchor")
        }
    });

    hierarchy.RegisterPage(new PageRecord
    {
        Url = "/settings/billing",
        Title = "Workspace Billing & Invoices",
        Breadcrumbs = new List<string> { "Home", "Settings", "Billing" },
        ActionableTargets = new List<ActionableTarget>
        {
            new("#payment-methods", "Saved Payment Methods", "anchor"),
            new("#invoice-history", "Past Invoices & Receipts", "anchor")
        }
    });

    hierarchy.RegisterPage(new PageRecord
    {
        Url = "/docs/api",
        Title = "Developer API Reference",
        Breadcrumbs = new List<string> { "Home", "Docs", "API" },
        ActionableTargets = new List<ActionableTarget>
        {
            new("#auth-header", "Authorization Bearer Token", "anchor"),
            new("#rate-limits", "Rate Limits & Throttling", "anchor")
        }
    });

    // Seed Sample Chunks for Micro Hybrid RAG
    searchStore.UpsertChunks(new[]
    {
        new ContentChunk("chk-1", "/pricing", "Enterprise SLA", "Pricing > Enterprise SLA",
            "Our Enterprise plan guarantees a 99.99% uptime SLA with 24/7 dedicated support and priority incident response within 15 minutes."),
        new ContentChunk("chk-2", "/pricing", "Payment Methods", "Pricing > FAQ",
            "We support automated credit card payments (Visa, MasterCard, Amex) and enterprise invoicing with Net-30 payment terms."),
        new ContentChunk("chk-3", "/settings/billing", "Billing Email", "Settings > Billing",
            "You can update your workspace billing notification email address under the Billing Contact section of the billing settings tab.")
    });
}

public record MessageRequest(string Message, string? CurrentUrl, ClientTelemetry? Telemetry);
