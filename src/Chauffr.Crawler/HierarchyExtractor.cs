using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Chauffr.Core.Models;

namespace Chauffr.Crawler;

public class HierarchyExtractor
{
    /// <summary>
    /// Extracts breadcrumbs, navigation links, and safe actionable targets before DOM sanitization.
    /// </summary>
    public (List<string> Breadcrumbs, List<ActionableTarget> Targets) ExtractMacroStructure(IHtmlDocument document, string currentUrl)
    {
        var breadcrumbs = ExtractBreadcrumbs(document);
        var targets = ExtractActionableTargets(document);

        return (breadcrumbs, targets);
    }

    private static List<string> ExtractBreadcrumbs(IHtmlDocument document)
    {
        var breadcrumbs = new List<string>();

        // Check semantic breadcrumb containers
        var breadcrumbContainer = document.QuerySelector("nav[aria-label*='breadcrumb' i], .breadcrumb, .breadcrumbs, ol[itemtype*='BreadcrumbList']");
        if (breadcrumbContainer != null)
        {
            var items = breadcrumbContainer.QuerySelectorAll("li, a, span[itemprop='name']");
            foreach (var item in items)
            {
                var text = item.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && !breadcrumbs.Contains(text) && text != "/" && text != ">")
                {
                    breadcrumbs.Add(text);
                }
            }
        }

        return breadcrumbs;
    }

    private static List<ActionableTarget> ExtractActionableTargets(IHtmlDocument document)
    {
        var targets = new List<ActionableTarget>();
        var seenSelectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Explicitly annotated targets (data-chauffr-target or legacy data-navguide-target)
        var explicitElements = document.QuerySelectorAll("[data-chauffr-target], [data-navguide-target]");
        foreach (var el in explicitElements)
        {
            var targetId = el.GetAttribute("data-chauffr-target") ?? el.GetAttribute("data-navguide-target") ?? el.Id;
            var selector = !string.IsNullOrEmpty(el.Id) ? $"#{el.Id}" : $"[data-chauffr-target='{targetId}']";
            var label = el.GetAttribute("aria-label") ?? el.TextContent?.Trim() ?? targetId;

            if (seenSelectors.Add(selector))
            {
                targets.Add(new ActionableTarget(selector, TruncateLabel(label ?? targetId), "custom"));
            }
        }

        // 2. Headings and sections with IDs (deep-linking anchors)
        var headingAndSections = document.QuerySelectorAll("h1[id], h2[id], h3[id], section[id]");
        foreach (var el in headingAndSections)
        {
            if (string.IsNullOrWhiteSpace(el.Id)) continue;
            var selector = $"#{el.Id}";
            var label = el.TextContent?.Trim() ?? el.Id;

            if (seenSelectors.Add(selector))
            {
                targets.Add(new ActionableTarget(selector, TruncateLabel(label), "anchor"));
            }
        }

        // 3. Read-only drawers / accordions / detail triggers
        var accordionElements = document.QuerySelectorAll("summary, [aria-expanded][data-drawer], [data-accordion-target]");
        foreach (var el in accordionElements)
        {
            string selector;
            if (!string.IsNullOrEmpty(el.Id))
            {
                selector = $"#{el.Id}";
            }
            else if (el.HasAttribute("data-drawer"))
            {
                selector = $"[data-drawer='{el.GetAttribute("data-drawer")}']";
            }
            else
            {
                continue;
            }

            var label = el.TextContent?.Trim() ?? "Expansion Drawer";
            if (seenSelectors.Add(selector))
            {
                targets.Add(new ActionableTarget(selector, TruncateLabel(label), "drawer"));
            }
        }

        return targets;
    }

    private static string TruncateLabel(string text, int maxLength = 60)
    {
        var clean = string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= maxLength ? clean : clean[..maxLength] + "...";
    }
}
