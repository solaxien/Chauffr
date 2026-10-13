using System.Text.RegularExpressions;

namespace Chauffr.Core.Safety;

public static partial class SelectorValidator
{
    // Regex for safe CSS selector characters (alphanumeric, dashes, underscores, dots, hashes, brackets for data attributes)
    private static readonly Regex SafeSelectorPattern = new(
        @"^[a-zA-Z0-9_\-\.\#\:\(\)\[\]\=\'"" ]+$",
        RegexOptions.Compiled);

    // Forbidden patterns that imply state mutation, payment, deletion, or form submission
    private static readonly Regex ForbiddenKeywordsPattern = new(
        @"(button\[type=['""]?submit['""]?\]|input|textarea|select|form|delete|destroy|remove|checkout|purchase|pay|buy|transfer|revoke|billing-change)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates whether a CSS selector is safe to interact with on the client side.
    /// </summary>
    public static bool IsSelectorSafe(string? selector, out string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            failureReason = "Selector cannot be null or whitespace.";
            return false;
        }

        if (selector.Length > 256)
        {
            failureReason = "Selector exceeds maximum length of 256 characters.";
            return false;
        }

        if (!SafeSelectorPattern.IsMatch(selector))
        {
            failureReason = "Selector contains illegal or potentially malicious characters.";
            return false;
        }

        if (ForbiddenKeywordsPattern.IsMatch(selector))
        {
            failureReason = $"Selector matches restricted mutating or sensitive UI patterns: '{selector}'.";
            return false;
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Checks whether an action tier is permitted for autonomous co-browsing.
    /// </summary>
    public static bool IsTierAllowed(ActionTier tier)
    {
        return tier is ActionTier.SafeNavigation or ActionTier.ReadOnlyExpansion;
    }
}
