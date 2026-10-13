namespace Chauffr.Core.Safety;

/// <summary>
/// Defines permission tiers for client-side co-browsing actions.
/// </summary>
public enum ActionTier
{
    /// <summary>
    /// Tier 1: Passive/read-only interactions such as smooth scrolling, visual pulse highlights, and route navigation.
    /// Safe by default; immediately dismissible by any user gesture.
    /// </summary>
    SafeNavigation = 1,

    /// <summary>
    /// Tier 2: Controlled UI expansions like opening an FAQ accordion or read-only preview drawer.
    /// Strictly restricted to pre-registered data-Chauffr-target items.
    /// </summary>
    ReadOnlyExpansion = 2,

    /// <summary>
    /// Tier 3: State-mutating actions (form submissions, button clicks, deleting data, checkout).
    /// Strictly prohibited in the co-browsing bridge to prevent prompt injection and unauthorized actions.
    /// </summary>
    MutatingAction = 3
}
