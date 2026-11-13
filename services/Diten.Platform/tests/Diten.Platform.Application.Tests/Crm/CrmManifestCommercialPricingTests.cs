using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-VW-W6 W6-BE-b + W6-WEB-b — the sixteen MOD-0156 §14 permission keys (price lists, guardrail rules, FX rates). W6-BE-b
// declared them API-only; W6-WEB-b ships screen D: ONE menu page COMMERCIALPRICING (Nav.Page.COMMERCIALPRICING, opened with
// the price lists' read key) whose actions are fifteen of them — the FX import has no screen yet and stays API-only. A
// definition only (the grant is W6-CFG), declared once each. CrmService pins the same sixteen in PricingPermissions.All.
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

    private static readonly string[] Families = ["commercial.price-lists.", "commercial.pricing-rules.", "commercial.fx-rates."];

    [Fact]
    public void The_sixteen_pricing_keys_are_the_screen_d_page_its_actions_and_the_one_api_only_key_declared_once_each()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "COMMERCIALPRICING");
        Assert.Equal(("/Commercial/Pricing", "commercial.price-lists.read", true), (page.RoutePath, page.RequiredPermission, page.IsNavigationVisible));
        Assert.All(page.Actions, a => Assert.False(string.IsNullOrWhiteSpace(a.DisplayName)));
        Assert.All(page.Actions.Where(a => a.PermissionKey.EndsWith(".publish", StringComparison.Ordinal)), a => Assert.True(a.IsDangerous));

        var api = (Manifest.ApiPermissions ?? []).Where(p => Families.Any(f => p.PermissionKey.StartsWith(f, StringComparison.Ordinal))).ToList();
        Assert.Equal(["commercial.fx-rates.import"], api.Select(p => p.PermissionKey));

        var declared = new[] { page.RequiredPermission }.Concat(page.Actions.Select(a => a.PermissionKey)).Concat(api.Select(p => p.PermissionKey)).ToList();
        Assert.Equal(Keys.OrderBy(k => k, StringComparer.Ordinal), declared.OrderBy(k => k, StringComparer.Ordinal));
        Assert.DoesNotContain(declared, k => k.EndsWith(".manage", StringComparison.Ordinal));                     // §14: no .manage key
    }

    [Fact]
    public void Only_screen_d_uses_a_pricing_key_and_the_module_identity_is_unchanged()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        var users = Manifest.Pages.Where(p => new[] { p.RequiredPermission }.Concat(p.Actions.Select(a => a.PermissionKey))
            .Any(k => Families.Any(f => k?.StartsWith(f, StringComparison.Ordinal) == true))).Select(p => p.PageCode).ToList();
        Assert.Equal(["COMMERCIALPRICING"], users);
        Assert.Single(Manifest.Pages, p => p.SortOrder == 75);
        var all = Manifest.ApiPermissions ?? [];
        Assert.Equal(all.Count, all.Select(p => p.PermissionKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
