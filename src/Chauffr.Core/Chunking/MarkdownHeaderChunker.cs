using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Chauffr.Core.Models;

namespace Chauffr.Core.Chunking;

public static partial class MarkdownHeaderChunker
{
    private static readonly Regex HeadingRegex = new(
        @"^(#{1,4})\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Chunks clean markdown by headings while preserving hierarchical breadcrumb context.
    /// </summary>
    public static List<ContentChunk> ChunkMarkdown(
        string url,
        string markdown,
        IReadOnlyList<string> pageBreadcrumbs,
        int maxTokensPerChunk = 600)
    {
        var chunks = new List<ContentChunk>();
        if (string.IsNullOrWhiteSpace(markdown)) return chunks;

        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count == 0)
        {
            // No headings found; treat whole body as single chunk or split by paragraphs
            chunks.Add(CreateChunk(url, "Overview", string.Join(" > ", pageBreadcrumbs), markdown.Trim()));
            return chunks;
        }

        var headingStack = new Stack<(int Level, string Title)>();

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            int level = match.Groups[1].Value.Length;
            string title = match.Groups[2].Value.Trim();

            // Maintain heading hierarchy stack
            while (headingStack.Count > 0 && headingStack.Peek().Level >= level)
            {
                headingStack.Pop();
            }
            headingStack.Push((level, title));

            int contentStart = match.Index + match.Length;
            int contentEnd = (i + 1 < matches.Count) ? matches[i + 1].Index : markdown.Length;
            string sectionContent = markdown[contentStart..contentEnd].Trim();

            if (string.IsNullOrWhiteSpace(sectionContent)) continue;

            var currentBreadcrumbs = new List<string>(pageBreadcrumbs);
            currentBreadcrumbs.AddRange(headingStack.Reverse().Select(h => h.Title));
            string trail = string.Join(" > ", currentBreadcrumbs);

            // If section content exceeds max tokens, split into paragraph blocks
            int estimatedTokens = EstimateTokenCount(sectionContent);
            if (estimatedTokens > maxTokensPerChunk)
            {
                var subChunks = SplitByParagraphs(sectionContent, maxTokensPerChunk);
                for (int subIdx = 0; subIdx < subChunks.Count; subIdx++)
                {
                    chunks.Add(CreateChunk(url, $"{title} (Part {subIdx + 1})", trail, subChunks[subIdx]));
                }
            }
            else
            {
                chunks.Add(CreateChunk(url, title, trail, sectionContent));
            }
        }

        return chunks;
    }

    private static ContentChunk CreateChunk(string url, string heading, string trail, string content)
    {
        string id = ComputeChunkId(url, heading, content);
        int tokens = EstimateTokenCount(content);
        return new ContentChunk(id, url, heading, trail, content, null, tokens);
    }

    private static List<string> SplitByParagraphs(string content, int maxTokens)
    {
        var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var subChunks = new List<string>();
        var currentBuilder = new StringBuilder();
        int currentTokens = 0;

        foreach (var p in paragraphs)
        {
            int pTokens = EstimateTokenCount(p);
            if (currentTokens + pTokens > maxTokens && currentBuilder.Length > 0)
            {
                subChunks.Add(currentBuilder.ToString().Trim());
                currentBuilder.Clear();
                currentTokens = 0;
            }

            currentBuilder.AppendLine(p).AppendLine();
            currentTokens += pTokens;
        }

        if (currentBuilder.Length > 0)
        {
            subChunks.Add(currentBuilder.ToString().Trim());
        }

        return subChunks;
    }

    public static int EstimateTokenCount(string text)
    {
        // Rough heuristic for English/code: ~4 chars per token
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    private static string ComputeChunkId(string url, string heading, string content)
    {
        string raw = $"{url}::{heading}::{content}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
