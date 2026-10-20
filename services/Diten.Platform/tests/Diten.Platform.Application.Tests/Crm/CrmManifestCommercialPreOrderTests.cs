using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-VW-W6 W6-BE-a — the CRM manifest declares the eleven MOD-0168 §14 pre-order keys as API permissions: a definition only
// (the grant is W6-CFG, the pages W6-WEB-a), declared once, beside the MOD-0156 pricing keys. CrmService pins the same eleven
// in PreOrderPermissions.All.
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
    public void The_eleven_pre_order_keys_are_declared_once_each_with_a_display_name()
    {
        var all = Manifest.ApiPermissions ?? [];
        var preOrder = all.Where(p => p.PermissionKey.StartsWith("commercial.pre-order", StringComparison.Ordinal)).ToList();

        Assert.Equal(Keys.OrderBy(k => k, StringComparer.Ordinal), preOrder.Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(preOrder, p => Assert.False(string.IsNullOrWhiteSpace(p.DisplayName)));
        Assert.Equal(16 + 11, all.Count(p => p.PermissionKey.StartsWith("commercial.", StringComparison.Ordinal)));
        Assert.Equal(all.Count, all.Select(p => p.PermissionKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
