using Chauffr.Core.Chunking;
using Chauffr.Core.Models;
using Chauffr.Indexing;
using Xunit;

namespace Chauffr.Tests.Unit;

public class ChunkingAndSearchTests
{
    [Fact]
    public void MarkdownHeaderChunker_ShouldSplitByHeadings_AndPreserveBreadcrumbs()
    {
        string markdown = @"
# Overview
Welcome to the API platform.

## Authentication
Use Bearer tokens to authenticate your requests.

### Rate Limiting
Requests are throttled to 100 per minute.
";
        var pageBreadcrumbs = new List<string> { "Docs", "Developer" };
        var chunks = MarkdownHeaderChunker.ChunkMarkdown("/docs/api", markdown, pageBreadcrumbs);

        Assert.Equal(3, chunks.Count);
        Assert.Contains("Overview", chunks[0].Heading);
        Assert.Equal("Docs > Developer > Overview", chunks[0].BreadcrumbTrail);

        Assert.Contains("Authentication", chunks[1].Heading);
        Assert.Equal("Docs > Developer > Overview > Authentication", chunks[1].BreadcrumbTrail);

        Assert.Contains("Rate Limiting", chunks[2].Heading);
        Assert.Equal("Docs > Developer > Overview > Authentication > Rate Limiting", chunks[2].BreadcrumbTrail);
    }

    [Fact]
    public void HybridSearchStore_ShouldRetrieveExactTokensViaBM25()
    {
        var store = new HybridSearchStore();
        store.UpsertChunks(new[]
        {
            new ContentChunk("c1", "/products", "Model X100", "Products", "Product SKU-99482 is our high-performance chip."),
            new ContentChunk("c2", "/products", "Model Y200", "Products", "Product SKU-11204 is designed for low power consumption."),
            new ContentChunk("c3", "/errors", "Error Codes", "Support", "Status ERR_TOKEN_EXPIRED occurs when JWT session expires.")
        });

        // Search for specific alphanumeric token that dense models often miss
        var results = store.SearchHybrid("ERR_TOKEN_EXPIRED");

        Assert.NotEmpty(results);
        Assert.Equal("c3", results.First().Chunk.Id);
        Assert.True(results.First().Bm25Score > 0);
    }

    [Fact]
    public void HierarchyBuilder_ShouldMatchNavigationalQueries()
    {
        var builder = new HierarchyBuilder();
        builder.RegisterPage(new PageRecord
        {
            Url = "/settings/billing",
            Title = "Billing & Invoices",
            Breadcrumbs = new List<string> { "Settings", "Account" },
            ActionableTargets = new List<ActionableTarget>
            {
                new("#update-payment", "Change Payment Method", "anchor")
            }
        });

        var matches = builder.SearchHierarchy("Where can I change payment method?");

        Assert.NotEmpty(matches);
        Assert.Equal("/settings/billing", matches.First().Node.Path);
        Assert.NotNull(matches.First().Target);
        Assert.Equal("#update-payment", matches.First().Target!.Selector);
    }
}
