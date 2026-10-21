using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-VW-W6 W6-BE-a + W6-WEB-a — the eleven MOD-0168 §14 pre-order keys. W6-BE-a declared them API-only; W6-WEB-a ships the
// screens, so each key is now the PREORDERS / PREORDERSETTINGS page or one of their actions (MOD-0168 §9 menu keys
// Nav.Page.PREORDERS / Nav.Page.PREORDERSETTINGS) — declared once each, none left in the API-only list beside the MOD-0156
// pricing keys (those stay API-only until W6-WEB-b). CrmService pins the same eleven in PreOrderPermissions.All.
public sealed class CrmManifestCommercialPreOrderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CrmManifestProvider().GetManifest();

    private static readonly string[] Keys =
    [
        "commercial.pre-orders.read", "commercial.pre-orders.read-all", "commercial.pre-orders.create", "commercial.pre-orders.submit",
        "commercial.pre-orders.approve", "commercial.pre-orders.cancel", "commercial.pre-orders.record-depot-response",
        "commercial.pre-orders.retry-transmission", "commercial.pre-orders.export", "commercial.pre-order-settings.read",
        "commercial.pre-order-settings.update"
    ];

    [Fact]
    public void The_eleven_pre_order_keys_are_the_two_pages_and_their_actions_declared_once_each()
    {
        var list = Assert.Single(Manifest.Pages, p => p.PageCode == "PREORDERS");
        Assert.Equal(("/Commercial/PreOrders", "commercial.pre-orders.read", true, "List"), (list.RoutePath, list.RequiredPermission, list.IsNavigationVisible, list.PageType));
        var settings = Assert.Single(Manifest.Pages, p => p.PageCode == "PREORDERSETTINGS");
        Assert.Equal(("/Commercial/PreOrderSettings", "commercial.pre-order-settings.read", true), (settings.RoutePath, settings.RequiredPermission, settings.IsNavigationVisible));

        var declared = new[] { list, settings }.SelectMany(p => new[] { p.RequiredPermission }.Concat(p.Actions.Select(a => a.PermissionKey))).ToList();
        Assert.Equal(Keys.OrderBy(k => k, StringComparer.Ordinal), declared.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(list.Actions.Concat(settings.Actions), a => Assert.False(string.IsNullOrWhiteSpace(a.DisplayName)));
        Assert.Contains(list.Actions, a => a.ActionCode == "CANCEL" && a.IsDangerous);

        // no pre-order key elsewhere in the manifest (not API-only, not on another page)
        var api = Manifest.ApiPermissions ?? [];
        Assert.DoesNotContain(api, p => p.PermissionKey.StartsWith("commercial.pre-order", StringComparison.Ordinal));
        // W6-WEB-b: the MOD-0156 keys are screen D's page + actions too; only the FX import (no screen yet) stays API-only
        Assert.Equal(["commercial.fx-rates.import"], api.Where(p => p.PermissionKey.StartsWith("commercial.", StringComparison.Ordinal)).Select(p => p.PermissionKey));
        var everywhere = Manifest.Pages.SelectMany(p => new[] { p.RequiredPermission }.Concat(p.Actions.Select(a => a.PermissionKey)))
            .Where(k => k.StartsWith("commercial.pre-order", StringComparison.Ordinal)).ToList();
        Assert.Equal(Keys.Length, everywhere.Count);
        Assert.Equal(api.Count, api.Select(p => p.PermissionKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // the two pages do not share their menu place with another CRM page
        Assert.Single(Manifest.Pages, p => p.SortOrder == list.SortOrder);
        Assert.Single(Manifest.Pages, p => p.SortOrder == settings.SortOrder);
    }
}
