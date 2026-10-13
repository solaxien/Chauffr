using System.Collections.Concurrent;
using Chauffr.Core.Models;
using Chauffr.Core.Safety;

namespace Chauffr.Indexing;

public interface IActionRegistry
{
    bool IsSelectorAllowed(string selector, string url);
    IReadOnlyList<ActionableTarget> GetTargetsForUrl(string url);
}

public class HierarchyBuilder : IActionRegistry
{
    private readonly ConcurrentDictionary<string, HierarchyNode> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<ActionableTarget>> _urlTargets = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPage(PageRecord page)
    {
        var node = new HierarchyNode
        {
            Path = page.Url,
            Title = page.Title,
            Breadcrumbs = page.Breadcrumbs,
            ActionableTargets = page.ActionableTargets,
            LastModified = page.LastModified
        };

        _routes[page.Url] = node;
        _urlTargets[page.Url] = page.ActionableTargets;
    }

    public HierarchyNode? GetNode(string url)
    {
        _routes.TryGetValue(url, out var node);
        return node;
    }

    public IReadOnlyList<HierarchyNode> GetAllNodes() => _routes.Values.ToList();

    public IReadOnlyList<ActionableTarget> GetTargetsForUrl(string url)
    {
        if (_urlTargets.TryGetValue(url, out var targets))
        {
            return targets;
        }
        return Array.Empty<ActionableTarget>();
    }

    public bool IsSelectorAllowed(string selector, string url)
    {
        // 1. Run basic zero-trust safety check
        if (!SelectorValidator.IsSelectorSafe(selector, out _))
        {
            return false;
        }

        // 2. Check if the selector is registered in the macro index for this URL
        if (_urlTargets.TryGetValue(url, out var targets))
        {
            if (targets.Any(t => t.Selector.Equals(selector, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // 3. Fallback: Allow registered heading IDs on the page
        return selector.StartsWith("#") && selector.Length > 2;
    }

    /// <summary>
    /// Searches the macro site hierarchy for matches against a navigational query.
    /// </summary>
    public List<(HierarchyNode Node, double Score, ActionableTarget? Target)> SearchHierarchy(string query)
    {
        var tokens = query.Split(new[] { ' ', '?', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim().ToLowerInvariant())
                          .Where(t => t.Length > 2)
                          .ToList();

        var results = new List<(HierarchyNode Node, double Score, ActionableTarget? Target)>();

        foreach (var node in _routes.Values)
        {
            double score = 0;
            ActionableTarget? bestTarget = null;

            // Check title match
            var lowerTitle = node.Title.ToLowerInvariant();
            foreach (var token in tokens)
            {
                if (lowerTitle.Contains(token)) score += 2.0;
                if (node.Path.ToLowerInvariant().Contains(token)) score += 1.5;
            }

            // Check breadcrumb matches
            foreach (var bc in node.Breadcrumbs)
            {
                var lowerBc = bc.ToLowerInvariant();
                foreach (var token in tokens)
                {
                    if (lowerBc.Contains(token)) score += 1.0;
                }
            }

            // Check actionable targets (headings, buttons, drawers)
            foreach (var target in node.ActionableTargets)
            {
                var lowerLabel = target.Label.ToLowerInvariant();
                double targetScore = 0;
                foreach (var token in tokens)
                {
                    if (lowerLabel.Contains(token)) targetScore += 3.0;
                }

                if (targetScore > 0 && (bestTarget == null || targetScore > score))
                {
                    bestTarget = target;
                    score += targetScore;
                }
            }

            if (score > 0)
            {
                results.Add((node, score, bestTarget));
            }
        }

        return results.OrderByDescending(r => r.Score).Take(5).ToList();
    }
}
