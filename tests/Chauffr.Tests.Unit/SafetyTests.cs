using System.Security;
using Chauffr.Api.Filters;
using Chauffr.Core.Models;
using Chauffr.Core.Safety;
using Chauffr.Indexing;
using Xunit;

namespace Chauffr.Tests.Unit;

public class SafetyTests
{
    [Theory]
    [InlineData("button[type='submit']")]
    [InlineData("input[name='password']")]
    [InlineData("form.checkout-form")]
    [InlineData("#delete-account-btn")]
    [InlineData(".destroy-database")]
    [InlineData("button.pay-now")]
    public void SelectorValidator_ShouldReject_MutatingAndSensitiveSelectors(string dangerousSelector)
    {
        bool isSafe = SelectorValidator.IsSelectorSafe(dangerousSelector, out string? reason);
        Assert.False(isSafe);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData("#enterprise-sla")]
    [InlineData("[data-Chauffr-target='faq-accordion']")]
    [InlineData("section#features")]
    [InlineData(".documentation-heading")]
    [InlineData("#auth-header")]
    public void SelectorValidator_ShouldAllow_SafeNavigationSelectors(string safeSelector)
    {
        bool isSafe = SelectorValidator.IsSelectorSafe(safeSelector, out string? reason);
        Assert.True(isSafe);
        Assert.Null(reason);
    }

    [Fact]
    public void ActionSafetyFilter_ShouldThrowSecurityException_ForTier3MutatingAction()
    {
        var hierarchy = new HierarchyBuilder();
        var filter = new ActionSafetyFilter(hierarchy);

        var mutatingAction = new CoBrowsingAction(
            Type: "SUBMIT_FORM",
            Tier: ActionTier.MutatingAction,
            Payload: new ActionPayloadData("/checkout", "#submit-order"),
            Security: new ActionSecurityMetadata(false, null, DateTime.UtcNow)
        );

        Assert.Throws<SecurityException>(() => filter.SanitizeAndValidate(mutatingAction));
    }

    [Fact]
    public void ActionSafetyFilter_ShouldThrowSecurityException_ForUnverifiedSelector()
    {
        var hierarchy = new HierarchyBuilder();
        var filter = new ActionSafetyFilter(hierarchy);

        // Selector not registered in hierarchy registry
        var unverifiedAction = new CoBrowsingAction(
            Type: "NAVIGATE_AND_HIGHLIGHT",
            Tier: ActionTier.SafeNavigation,
            Payload: new ActionPayloadData("/pricing", ".unregistered-random-selector"),
            Security: new ActionSecurityMetadata(false, null, DateTime.UtcNow)
        );

        Assert.Throws<SecurityException>(() => filter.SanitizeAndValidate(unverifiedAction));
    }

    [Fact]
    public void ActionSafetyFilter_ShouldPassAndAttachHash_ForVerifiedSafeAction()
    {
        var hierarchy = new HierarchyBuilder();
        hierarchy.RegisterPage(new PageRecord
        {
            Url = "/pricing",
            Title = "Pricing",
            ActionableTargets = new List<ActionableTarget>
            {
                new("#enterprise-sla", "Enterprise SLA", "anchor")
            }
        });

        var filter = new ActionSafetyFilter(hierarchy);

        var validAction = new CoBrowsingAction(
            Type: "NAVIGATE_AND_HIGHLIGHT",
            Tier: ActionTier.SafeNavigation,
            Payload: new ActionPayloadData("/pricing", "#enterprise-sla"),
            Security: new ActionSecurityMetadata(false, null, DateTime.UtcNow)
        );

        var secured = filter.SanitizeAndValidate(validAction);

        Assert.NotNull(secured.Security.TargetHash);
        Assert.StartsWith("sha256-", secured.Security.TargetHash);
    }
}
