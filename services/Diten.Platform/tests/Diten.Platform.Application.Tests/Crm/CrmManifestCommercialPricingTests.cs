using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-VW-W6 W6-BE-b — the CRM manifest declares the sixteen MOD-0156 §14 permission keys (price lists, guardrail rules,
// FX rates) as API permissions: a definition only (the grant is W6-CFG), no page yet (W6-WEB-b), the module identity and
// the existing pages unchanged. CrmService pins the same sixteen in PricingPermissions.All.
public sealed class CrmManifestCommercialPricingTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CrmManifestProvider().GetManifest();

    private static readonly string[] Keys =
    [
        "commercial.price-lists.read", "commercial.price-lists.create", "commercial.price-lists.update", "commercial.price-lists.import",
        "commercial.price-lists.submit", "commercial.price-lists.publish", "commercial.price-lists.export",
        "commercial.pricing-rules.read", "commercial.pricing-rules.create", "commercial.pricing-rules.update", "commercial.pricing-rules.submit",
        "commercial.pricing-rules.publish",
        "commercial.fx-rates.read", "commercial.fx-rates.create", "commercial.fx-rates.update", "commercial.fx-rates.import"
    ];

    [Fact]
    public void The_sixteen_pricing_keys_are_declared_once_each_with_a_display_name()
    {
        // W6-BE-a: the namespace also holds MOD-0168's pre-order keys (CrmManifestCommercialPreOrderTests) — the pricing families here
        string[] families = ["commercial.price-lists.", "commercial.pricing-rules.", "commercial.fx-rates."];
        var commercial = (Manifest.ApiPermissions ?? []).Where(p => families.Any(f => p.PermissionKey.StartsWith(f, StringComparison.Ordinal))).ToList();

        Assert.Equal(Keys.OrderBy(k => k, StringComparer.Ordinal), commercial.Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(commercial, p => Assert.False(string.IsNullOrWhiteSpace(p.DisplayName)));
        Assert.DoesNotContain(commercial, p => p.PermissionKey.EndsWith(".manage", StringComparison.Ordinal));   // §14: no .manage key
    }

    [Fact]
    public void No_page_uses_a_pricing_key_yet_and_the_module_identity_is_unchanged()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        // W6-WEB-a: the pre-order pages (commercial.pre-orders.* / .pre-order-settings.*) exist; no page or action uses a PRICING key yet
        string[] families = ["commercial.price-lists.", "commercial.pricing-rules.", "commercial.fx-rates."];
        var pageKeys = Manifest.Pages.SelectMany(p => new[] { p.RequiredPermission }.Concat(p.Actions.Select(a => a.PermissionKey))).ToList();
        Assert.DoesNotContain(pageKeys, k => families.Any(f => k?.StartsWith(f, StringComparison.Ordinal) == true));
        var all = Manifest.ApiPermissions ?? [];
        Assert.Equal(all.Count, all.Select(p => p.PermissionKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
