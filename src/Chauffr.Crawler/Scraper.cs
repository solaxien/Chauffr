using AngleSharp.Html.Parser;
using Microsoft.Playwright;
using Chauffr.Core.Chunking;
using Chauffr.Core.Models;

namespace Chauffr.Crawler;

public class Scraper : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly HtmlParser _htmlParser = new();
    private readonly HierarchyExtractor _hierarchyExtractor = new();
    private readonly DomCleaner _domCleaner = new();

    public async Task InitializeAsync()
    {
        if (_playwright != null) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--blink-settings=imagesEnabled=false" // Resource optimization
            }
        });
    }

    /// <summary>
    /// Fetches a dynamic page, extracts hierarchy, sanitizes DOM, and creates chunked content.
    /// </summary>
    public async Task<PageRecord?> ScrapePageAsync(string url, DateTime? lastModified = null)
    {
        if (_browser == null) await InitializeAsync();

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Chauffr-Autonomous-Crawler/1.0 (+https://github.com/Chauffr/ai)"
        });

        var page = await context.NewPageAsync();

        try
        {
            // Route abort for media, stylesheets, and fonts to accelerate crawling
            await page.RouteAsync("**/*", async route =>
            {
                var resourceType = route.Request.ResourceType;
                if (resourceType is "image" or "media" or "font")
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            // Navigate and wait for DOM content loaded
            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            if (response == null || !response.Ok)
            {
                Console.WriteLine($"[Scraper] Failed to fetch {url} (Status: {response?.Status})");
                return null;
            }

            // Brief wait for SPA hydration
            await page.WaitForTimeoutAsync(800);

            var pageTitle = await page.TitleAsync();
            var renderedHtml = await page.ContentAsync();

            // Load into AngleSharp DOM
            using var document = await _htmlParser.ParseDocumentAsync(renderedHtml);

            // Pass 1: Extract macro hierarchy (breadcrumbs and actionable targets) BEFORE sanitization
            var (breadcrumbs, actionableTargets) = _hierarchyExtractor.ExtractMacroStructure(document, url);

            // Pass 2: Clean DOM and convert to clean Markdown
            var (cleanMarkdown, contentHash) = _domCleaner.CleanAndConvertToMarkdown(document);

            // Pass 3: Heading-aware chunking
            var chunks = MarkdownHeaderChunker.ChunkMarkdown(url, cleanMarkdown, breadcrumbs);

            return new PageRecord
            {
                Url = url,
                Title = pageTitle,
                LastModified = lastModified,
                ContentHash = contentHash,
                CleanMarkdown = cleanMarkdown,
                Breadcrumbs = breadcrumbs,
                ActionableTargets = actionableTargets,
                Chunks = chunks
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scraper] Error scraping {url}: {ex.Message}");
            return null;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        GC.SuppressFinalize(this);
    }
}
