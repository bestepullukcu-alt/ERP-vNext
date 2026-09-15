using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1b (DCP-004 §8 row 1b), revised by DCP-004 "Decision amendment 2026-09-15" (BL-410) — the Görev Merkezi manifest
// mirrors the REAL tenant route, is a BASELINE module and its page needs NO key, so every tenant user sees it. The
// inbox key stays declared but no endpoint enforces it. The permission oracle is reflected off the constants class,
// so a hand-typed string literal in the manifest fails here.
public sealed class WorkAggregationManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new WorkAggregationManifestProvider().GetManifest();

    // The real tenant routes. The page needs no key (amendment point 2).
    private static readonly HashSet<string> ExpectedRoutes = new(StringComparer.Ordinal) { "/WorkCenterNext" };

    // Zero-drift oracle: every permission constant declared by WC-1.
    private static readonly HashSet<string> KnownPermissionKeys = typeof(WorkAggregationPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Declares_a_clean_slug_baseline_module_identity()
    {
        Assert.Equal("work-aggregation", Manifest.ModuleCode);
        Assert.Equal("Work Aggregation", Manifest.ModuleName);
        Assert.Equal("Görev Merkezi / Task Center", Manifest.DisplayName);
        Assert.Equal("Workspace", Manifest.Domain);       // pack DEC-5
        Assert.Equal("DitenPlatform", Manifest.Service);
        // The tenant menu reads only assignable catalog rows (ModuleCatalogRepository.GetAssignableAsync), so a
        // baseline module that stopped being assignable would vanish from every sidebar.
        Assert.True(Manifest.IsTenantAssignable);
        Assert.True(Manifest.IsBaseline);                 // amendment 2026-09-15 — every tenant user, no entitlement
        Assert.Equal("bx-been-here", Manifest.Icon);
        Assert.Equal(10, Manifest.SortOrder);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Governance_identity_never_leaks_into_the_manifest()
    {
        // The governance identity is documentation-only; it must never appear in runtime values.
        var values = new[] { Manifest.ModuleCode, Manifest.ModuleName, Manifest.DisplayName, Manifest.Domain, Manifest.Service }
            .Concat(Manifest.Pages.Select(p => p.PageCode))
            .Concat(Manifest.Pages.Select(p => p.RoutePath));

        Assert.All(values, v =>
        {
            Assert.DoesNotContain("CAND-CAP", v, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD-", v, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Pages_mirror_the_tenant_route_exactly_and_need_no_key()
    {
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.True(manifestRoutes.SetEquals(ExpectedRoutes),
            "Manifest pages must mirror the real tenant routes exactly (both directions).");

        // A key on the page hides Görev Merkezi from every user who lacks it (the sidebar and Ctrl+K gate on it).
        Assert.All(Manifest.Pages, page => Assert.True(
            string.IsNullOrWhiteSpace(page.RequiredPermission),
            $"Page {page.PageCode} requires '{page.RequiredPermission}', so users without it lose the menu entry."));
    }

    [Fact]
    public void Every_declared_permission_is_a_real_WorkAggregationPermissions_constant()
    {
        Assert.NotEmpty(KnownPermissionKeys);

        foreach (var page in Manifest.Pages.Where(p => !string.IsNullOrWhiteSpace(p.RequiredPermission)))
        {
            Assert.Contains(page.RequiredPermission, KnownPermissionKeys);
        }

        foreach (var action in Manifest.Pages.SelectMany(p => p.Actions))
        {
            Assert.Contains(action.PermissionKey, KnownPermissionKeys);
        }
    }

    [Fact]
    public void The_inbox_key_stays_declared_but_no_endpoint_enforces_it()
    {
        // Declared: the catalog→Auth sync and the entitled-module key pull keep knowing it as this module's key.
        var declared = Assert.Single(Manifest.Pages.SelectMany(p => p.Actions));
        Assert.Equal(WorkAggregationPermissions.InboxView, declared.PermissionKey);

        // Unenforced: no [HasPermission] anywhere in the Platform API names it. If one comes back, the Task Center
        // closes again for every user the sync never granted it to (every non-Admin role today).
        var enforced = HasPermissionReflector.CollectPermissionKeys(typeof(WorkItemsController).Assembly);
        Assert.DoesNotContain(WorkAggregationPermissions.InboxView, enforced, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Page_is_a_visible_top_level_nav_entry_whose_only_action_is_no_button()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("WORKCENTER", page.PageCode);
        Assert.True(page.IsNavigationVisible);
        Assert.Null(page.ParentPageCode);
        Assert.Equal("List", page.PageType);

        // The declaration action renders nowhere: not a toolbar button, not a row action. Commands go through the
        // work-item action endpoint, each under its source module's own key.
        var action = Assert.Single(page.Actions);
        Assert.Equal("INBOX_VIEW", action.ActionCode);
        Assert.Equal("View", action.ActionType);
        Assert.False(action.IsToolbarAction);
        Assert.False(action.IsRowAction);
        Assert.False(action.IsDangerous);
        // No new text: the declaration reuses the page's own display name.
        Assert.Equal(page.DisplayName, action.DisplayName);
    }

    [Fact]
    public void Page_codes_and_routes_are_unique_so_the_reconcile_cannot_skip_one()
    {
        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var routes = Manifest.Pages.Select(p => p.RoutePath).ToList();
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
