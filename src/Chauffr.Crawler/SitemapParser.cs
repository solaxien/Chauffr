using System.Xml.Linq;

namespace Chauffr.Crawler;

public record SitemapEntry(string Url, DateTime? LastModified);

public class SitemapParser
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private readonly HttpClient _httpClient;

    public SitemapParser(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<List<SitemapEntry>> ParseAsync(string sitemapUrl, CancellationToken cancellationToken = default)
    {
        var entries = new List<SitemapEntry>();

        try
        {
            var xmlContent = await _httpClient.GetStringAsync(sitemapUrl, cancellationToken);
            var doc = XDocument.Parse(xmlContent);

            // 1. Check if this is a sitemapindex pointing to sub-sitemaps
            var sitemaps = doc.Descendants(SitemapNs + "sitemap").ToList();
            if (sitemaps.Count > 0)
            {
                foreach (var sitemap in sitemaps)
                {
                    var loc = sitemap.Element(SitemapNs + "loc")?.Value.Trim();
                    if (!string.IsNullOrEmpty(loc))
                    {
                        var subEntries = await ParseAsync(loc, cancellationToken);
                        entries.AddRange(subEntries);
                    }
                }
                return entries;
            }

            // 2. Parse standard url entries
            var urls = doc.Descendants(SitemapNs + "url");
            foreach (var urlNode in urls)
            {
                var loc = urlNode.Element(SitemapNs + "loc")?.Value.Trim();
                if (string.IsNullOrEmpty(loc)) continue;

                DateTime? lastMod = null;
                var lastModStr = urlNode.Element(SitemapNs + "lastmod")?.Value;
                if (!string.IsNullOrEmpty(lastModStr) && DateTime.TryParse(lastModStr, out var parsedDate))
                {
                    lastMod = parsedDate.ToUniversalTime();
                }

                entries.Add(new SitemapEntry(loc, lastMod));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SitemapParser] Error reading sitemap from {sitemapUrl}: {ex.Message}");
        }

        return entries;
    }
}
