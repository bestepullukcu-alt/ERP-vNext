using Diten.InventoryService.Api.ModuleRegistration;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// FU-0174-CAT-01 (WP-0174-P2, PKS-001 §4 "one owner per namespace"): every permission key a manifest of this service
/// declares — page, action or <c>ApiPermissions</c> — is in the declaring module's OWN namespace. The AuthService catalog
/// sync sets a key's Module from the last manifest that synced it, so a foreign key in a manifest silently moves the
/// key to that module (measured live on 2026-10-06: <c>inventory.stock.read</c> → <c>lot-serial-tracking</c>).
/// </summary>
public sealed class ManifestNamespaceOwnershipTests
{
    public static TheoryData<string, string> Providers => new()
    {
        { InventoryLedgerManifestProvider.ModuleCode, "inventory." },
        { LotSerialTrackingManifestProvider.ModuleCode, "trace." }
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_key_of_the_manifest_is_in_the_module_namespace(string moduleCode, string ownPrefix)
    {
        IModuleManifestProvider provider = moduleCode == LotSerialTrackingManifestProvider.ModuleCode
            ? new LotSerialTrackingManifestProvider()
            : new InventoryLedgerManifestProvider();
        var manifest = provider.GetManifest();
        Assert.Equal(moduleCode, manifest.ModuleCode);

        var keys = manifest.Pages.Select(p => ($"page {p.PageCode}", p.RequiredPermission))
            .Concat(manifest.Pages.SelectMany(p => p.Actions.Select(a => ($"action {p.PageCode}.{a.ActionCode}", a.PermissionKey))))
            .Concat((manifest.ApiPermissions ?? []).Select(a => ("api permission", a.PermissionKey)))
            .ToList();
        Assert.True(keys.Count >= 20, $"{moduleCode}: too few keys found ({keys.Count}) — the scan would be vacuous");

        var foreign = keys.Where(k => !k.Item2.StartsWith(ownPrefix, StringComparison.Ordinal)).Select(k => $"{k.Item1}: {k.Item2}").ToList();
        Assert.True(foreign.Count == 0, $"{moduleCode} declares keys outside '{ownPrefix}*':\n" + string.Join("\n", foreign));
    }

    [Fact]
    public void The_lot_module_declares_no_inventory_key_and_the_ledger_module_no_trace_key()
    {
        static IEnumerable<string> Keys(IModuleManifestProvider provider)
        {
            var m = provider.GetManifest();
            return m.Pages.Select(p => p.RequiredPermission)
                .Concat(m.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
                .Concat((m.ApiPermissions ?? []).Select(a => a.PermissionKey));
        }

        Assert.DoesNotContain(Keys(new LotSerialTrackingManifestProvider()), k => k.StartsWith("inventory.", StringComparison.Ordinal));
        Assert.DoesNotContain(Keys(new InventoryLedgerManifestProvider()), k => k.StartsWith("trace.", StringComparison.Ordinal));
    }

    /// <summary>
    /// WP-0176-F1 (pack §14.1, FU-0174-CAT-01): the MOD-0176 manifest declares ONLY <c>expiry.*</c> keys — exactly the eight F1 keys, as
    /// <c>ApiPermissions</c> (no page yet). K3 sabotage (e): a <c>quarantine.*</c> key in it → red.
    /// </summary>
    [Fact]
    public void The_expiry_module_declares_only_expiry_keys()
    {
        var manifest = new Diten.InventoryService.Api.Expiry.ExpiryFefoManifestProvider().GetManifest();
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(a => a.PermissionKey))
            .ToList();

        Assert.Equal("EXPIRY-FEFO-MANAGEMENT", manifest.ModuleCode);
        var foreign = keys.Where(k => !k.StartsWith("expiry.", StringComparison.Ordinal)).ToList();
        Assert.True(foreign.Count == 0, "EXPIRY-FEFO-MANAGEMENT declares keys outside 'expiry.*':\n" + string.Join("\n", foreign));
        Assert.Equal(Diten.InventoryService.Infrastructure.Authorization.ExpiryPermissions.Implemented.OrderBy(k => k, StringComparer.Ordinal),
            keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// WP-0177-R1 (pack §14.1, FU-0174-CAT-01): the MOD-0177 manifest declares ONLY <c>recall.*</c> keys — exactly the thirteen R1 keys, as
    /// <c>ApiPermissions</c> (no page yet). K3 sabotage (e): a <c>quarantine.*</c> key in it → red.
    /// </summary>
    [Fact]
    public void The_recall_module_declares_only_recall_keys()
    {
        var manifest = new Diten.InventoryService.Api.Recall.RecallReadinessManifestProvider().GetManifest();
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(a => a.PermissionKey))
            .ToList();

        Assert.Equal("RECALL-READINESS", manifest.ModuleCode);
        var foreign = keys.Where(k => !k.StartsWith("recall.", StringComparison.Ordinal)).ToList();
        Assert.True(foreign.Count == 0, "RECALL-READINESS declares keys outside 'recall.*':\n" + string.Join("\n", foreign));
        Assert.Equal(Diten.InventoryService.Infrastructure.Authorization.RecallPermissions.R1.OrderBy(k => k, StringComparer.Ordinal),
            keys.OrderBy(k => k, StringComparer.Ordinal));
    }
}
