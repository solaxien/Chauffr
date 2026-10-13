using System.Security.Cryptography;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ReverseMarkdown;

namespace Chauffr.Crawler;

public class DomCleaner
{
    private readonly Converter _converter;

    public DomCleaner()
    {
        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.Drop,
            RemoveComments = true,
            SmartHrefHandling = true
        };
        _converter = new Converter(config);
    }

    /// <summary>
    /// Strips non-content chrome (scripts, styles, footer, ads) and converts the main body to clean Markdown.
    /// </summary>
    public (string Markdown, string ContentHash) CleanAndConvertToMarkdown(IHtmlDocument document)
    {
        // 1. Remove non-content elements
        var elementsToRemove = document.QuerySelectorAll(
            "script, style, noscript, iframe, svg, footer, " +
            "[aria-modal='true'], [role='dialog'], " +
            ".cookie-banner, #cookie-consent, .advertisement, [id*='cookie'], [class*='cookie']"
        );

        foreach (var el in elementsToRemove)
        {
            el.Remove();
        }

        // 2. Select main content container (fallback to body)
        var mainContainer = document.QuerySelector("main, article, #content, .content, [role='main']") ?? document.Body;
        var htmlContent = mainContainer?.InnerHtml ?? document.DocumentElement.InnerHtml;

        // 3. Convert to Markdown
        var markdown = _converter.Convert(htmlContent);

        // Clean up excessive whitespace/newlines
        markdown = NormalizeMarkdown(markdown);

        // 4. Compute SHA-256 content hash
        string hash = ComputeSha256(markdown);

        return (markdown, hash);
    }

    public static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeMarkdown(string md)
    {
        var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        int consecutiveEmptyLines = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                consecutiveEmptyLines++;
                if (consecutiveEmptyLines <= 2)
                {
                    sb.AppendLine();
                }
            }
            else
            {
                consecutiveEmptyLines = 0;
                sb.AppendLine(trimmed);
            }
        }

        return sb.ToString().Trim();
    }
}
