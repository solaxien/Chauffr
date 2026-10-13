using System.Security;
using System.Security.Cryptography;
using System.Text;
using Chauffr.Core.Models;
using Chauffr.Core.Safety;
using Chauffr.Indexing;

namespace Chauffr.Api.Filters;

public class ActionSafetyFilter
{
    private readonly IActionRegistry _actionRegistry;

    public ActionSafetyFilter(IActionRegistry actionRegistry)
    {
        _actionRegistry = actionRegistry;
    }

    /// <summary>
    /// Validates and sanitizes a co-browsing action before it is dispatched to the client.
    /// </summary>
    public CoBrowsingAction SanitizeAndValidate(CoBrowsingAction action)
    {
        // 1. Permission Tier check
        if (!SelectorValidator.IsTierAllowed(action.Tier))
        {
            throw new SecurityException($"Action tier '{action.Tier}' is prohibited. Co-browsing cannot execute state-mutating actions.");
        }

        var payload = action.Payload;

        // 2. Target Selector safety check
        if (!string.IsNullOrEmpty(payload.TargetSelector))
        {
            if (!SelectorValidator.IsSelectorSafe(payload.TargetSelector, out var reason))
            {
                throw new SecurityException($"Unsafe target selector rejected: {reason}");
            }

            // Verify selector against macro registry whitelist
            if (!_actionRegistry.IsSelectorAllowed(payload.TargetSelector, payload.Url))
            {
                throw new SecurityException($"Target selector '{payload.TargetSelector}' is not in the verified navigation whitelist for URL '{payload.Url}'.");
            }
        }

        // 3. Open Drawer / Modal selector safety check
        if (!string.IsNullOrEmpty(payload.OpenDrawerSelector))
        {
            if (!SelectorValidator.IsSelectorSafe(payload.OpenDrawerSelector, out var reason))
            {
                throw new SecurityException($"Unsafe drawer selector rejected: {reason}");
            }

            if (!_actionRegistry.IsSelectorAllowed(payload.OpenDrawerSelector, payload.Url))
            {
                throw new SecurityException($"Drawer selector '{payload.OpenDrawerSelector}' is not in the verified whitelist.");
            }
        }

        // 4. Generate integrity hash
        string targetHash = ComputeSecurityHash(payload.Url, payload.TargetSelector, action.Tier);

        var securedMetadata = new ActionSecurityMetadata(
            RequiresConfirmation: false,
            TargetHash: targetHash,
            TimestampUtc: DateTime.UtcNow
        );

        return action with { Security = securedMetadata };
    }

    private static string ComputeSecurityHash(string url, string? selector, ActionTier tier)
    {
        string raw = $"{url}::{selector ?? ""}::{(int)tier}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return "sha256-" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
