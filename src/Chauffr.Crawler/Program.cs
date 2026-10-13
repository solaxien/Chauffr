namespace Chauffr.Crawler;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Chauffr Autonomous Delta Ingestion Pipeline ===");

        string sitemapUrl = args.Length > 0 && args[0].StartsWith("http")
            ? args[0]
            : "https://example.com/sitemap.xml";

        Console.WriteLine($"Starting ingestion for: {sitemapUrl}");

        var parser = new SitemapParser();
        var entries = await parser.ParseAsync(sitemapUrl);

        Console.WriteLine($"Discovered {entries.Count} entries in sitemap.");

        await using var scraper = new Scraper();
        await scraper.InitializeAsync();

        foreach (var entry in entries)
        {
            Console.WriteLine($"Processing {entry.Url} (LastMod: {entry.LastModified?.ToString("u") ?? "N/A"})...");
            var pageRecord = await scraper.ScrapePageAsync(entry.Url, entry.LastModified);

            if (pageRecord != null)
            {
                Console.WriteLine($"  -> Extracted {pageRecord.Chunks.Count} chunks, {pageRecord.ActionableTargets.Count} actionable targets (Hash: {pageRecord.ContentHash[..8]}...)");
            }
        }

        Console.WriteLine("Ingestion completed.");
    }
}
