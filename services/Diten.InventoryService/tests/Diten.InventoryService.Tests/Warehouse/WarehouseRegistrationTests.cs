using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Warehouse;
using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Tests.Architecture;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — how the slice is registered (pack §5 seams, §14), measured on the production types and sources:
/// the manifest declares ONLY <c>warehouse.*</c> keys — exactly the ones the DL-1 controller enforces — under the central-audit source module
/// (FU-0174-CAT-01, PKS-001 §4); the location-role controller keys every action with them; <c>/api/warehouse</c> is a scoped base path of the
/// middleware; the warehouse code names no ledger store, no stock total and no <c>GOODS_ISSUE_SALES</c> (pack §2 boundary rules, AC-6 / AC-31
/// guard for what DL-1 adds).
/// </summary>
public sealed class WarehouseRegistrationTests
{
    [Fact]
    public void The_manifest_declares_only_the_warehouse_keys_the_controller_enforces()
    {
        var manifest = new PutawayPickingPackingManifestProvider().GetManifest();
        Assert.Equal("PUTAWAY-PICKING-PACKING", manifest.ModuleCode);
        Assert.Equal(WarehouseCentralAudit.Module, manifest.ModuleCode);
        Assert.Equal(178, manifest.SortOrder);

        // DL-3: the keys are declared by the pages and their actions; only the frozen outbound read stays an API permission
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
            .Distinct(StringComparer.Ordinal).ToList();
        Assert.All(keys, k => Assert.StartsWith("warehouse.", k, StringComparison.Ordinal));
        Assert.All(manifest.ApiPermissions ?? [], p => Assert.Equal("Tenant", p.Scope));
        Assert.Equal(WarehousePermissions.Implemented.Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));

        var enforced = typeof(WarehouseLocationRolesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => m.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Replace("Permission:", string.Empty))
            .ToList();
        Assert.Equal(6, enforced.Count);
        Assert.All(enforced, p => Assert.NotNull(p));
        Assert.Equal(WarehousePermissions.Dl1.Order(StringComparer.Ordinal), enforced.Select(p => p!).Distinct().Order(StringComparer.Ordinal));

        // DL-2: every key the warehouse controllers enforce is declared — and nothing else
        var allEnforced = typeof(WarehouseLocationRolesController).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(WarehouseLocationRolesController).Namespace && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => m.GetCustomAttribute<HasPermissionAttribute>()!.Policy!.Replace("Permission:", string.Empty))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(keys.Order(StringComparer.Ordinal), allEnforced.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC-20 (pack §11.1, §11.3, §14.4): two tenant pages under <c>/Warehouse/</c> (never <c>/Platform/</c> — the route derives the scope), in the
    /// menu, and one action per real §11.3 button with its key; the action types are the Platform's own enum names (an unknown name silently
    /// becomes Toolbar in the reconcile); only <c>warehouse.outbound-shipments.read</c> stays API-only.
    /// </summary>
    [Fact]
    public void AC20_the_manifest_declares_the_two_S1_pages_and_one_action_per_button()
    {
        var manifest = new PutawayPickingPackingManifestProvider().GetManifest();

        var s1 = manifest.Pages.Where(p => p.PageCode is "PUTAWAYTASKS" or "WAREHOUSELOCATIONROLES").ToList();
        Assert.Equal(new[] { ("PUTAWAYTASKS", "/Warehouse/PutawayTasks", WarehousePermissions.PutawayTasksRead), ("WAREHOUSELOCATIONROLES", "/Warehouse/LocationRoles", WarehousePermissions.LocationRolesRead) },
            s1.Select(p => (p.PageCode, p.RoutePath, p.RequiredPermission)));
        Assert.All(s1, p =>
        {
            Assert.True(p.IsNavigationVisible);
            Assert.Null(p.ParentPageCode);
            Assert.DoesNotContain("/Platform/", p.RoutePath, StringComparison.OrdinalIgnoreCase);
            Assert.All(p.Actions, a => Assert.Contains(a.ActionType, new[] { "Toolbar", "RowAction", "BulkAction" }));
        });

        var actions = s1.SelectMany(p => p.Actions).ToDictionary(a => a.ActionCode, a => (a.PermissionKey, a.IsDangerous));
        Assert.Equal(new Dictionary<string, (string, bool)>
        {
            ["PUTAWAY_TASK_CREATE"] = (WarehousePermissions.PutawayTasksCreate, false),
            ["PUTAWAY_TASK_UPDATE"] = (WarehousePermissions.PutawayTasksUpdate, false),
            ["PUTAWAY_TASK_ASSIGN"] = (WarehousePermissions.PutawayTasksUpdate, false),
            ["PUTAWAY_TASK_CONFIRM"] = (WarehousePermissions.PutawayTasksConfirm, false),
            ["PUTAWAY_TASK_RETRY"] = (WarehousePermissions.PutawayTasksConfirm, false),
            ["PUTAWAY_TASK_RETURNED"] = (WarehousePermissions.PutawayTasksConfirm, false),
            ["PUTAWAY_TASK_RESOLVE_INTEGRITY"] = (WarehousePermissions.PutawayTasksResolve, true),
            ["PUTAWAY_TASK_CLOSE_SHORT"] = (WarehousePermissions.PutawayTasksConfirm, false),
            ["PUTAWAY_TASK_CANCEL"] = (WarehousePermissions.PutawayTasksCancel, true),
            ["PUTAWAY_TASK_CLAIM"] = (WarehousePermissions.PutawayTasksConfirm, false),
            ["LOCATION_ROLE_CREATE"] = (WarehousePermissions.LocationRolesManage, false),
            ["LOCATION_ROLE_UPDATE"] = (WarehousePermissions.LocationRolesManage, false),
            ["LOCATION_ROLE_DELETE"] = (WarehousePermissions.LocationRolesManage, true),
            ["LOCATION_ROLE_BULK_DELETE"] = (WarehousePermissions.LocationRolesManage, true)
        }, actions);

        // DL-6: the S2 keys that DL-4 / DL-5 declared as API permissions moved onto the S2 pages (AC34_… below); none is API-only any more
        Assert.Empty(manifest.ApiPermissions ?? []);
    }

    /// <summary>
    /// AC-34 (pack §11.1, §11.3, §14.4; DL-6): the three S2 pages under <c>/Warehouse/</c> (never <c>/Platform/</c>) — OUTBOUNDSHIPMENTS and PICKTASKS in
    /// the menu, PACKINGSTATION not (reached from a pick task / an outbound) with <c>warehouse.packages.read</c> as its page key (NOT the outbound read —
    /// the operator packs without the ship-to, Q-10a; sabotage (do)) — and one action per real §11.3 S2 button with its key; the frozen outbound read
    /// MVP-6 uses is the OUTBOUNDSHIPMENTS page key, so nothing is left API-only.
    /// </summary>
    [Fact]
    public void AC34_the_manifest_declares_the_three_S2_pages_and_one_action_per_S2_button()
    {
        var manifest = new PutawayPickingPackingManifestProvider().GetManifest();

        var s2 = manifest.Pages.Where(p => p.PageCode is "OUTBOUNDSHIPMENTS" or "PICKTASKS" or "PACKINGSTATION").ToList();
        Assert.Equal(new[]
            {
                ("OUTBOUNDSHIPMENTS", "/Warehouse/OutboundShipments", WarehousePermissions.OutboundShipmentsRead, true),
                ("PICKTASKS", "/Warehouse/PickTasks", WarehousePermissions.PickTasksRead, true),
                ("PACKINGSTATION", "/Warehouse/Packing/{outboundId}", WarehousePermissions.PackagesRead, false)
            },
            s2.Select(p => (p.PageCode, p.RoutePath, p.RequiredPermission, p.IsNavigationVisible)));
        Assert.Equal(5, manifest.Pages.Count);
        Assert.All(s2, p =>
        {
            Assert.DoesNotContain("/Platform/", p.RoutePath, StringComparison.OrdinalIgnoreCase);
            Assert.All(p.Actions, a => Assert.Contains(a.ActionType, new[] { "Toolbar", "RowAction", "BulkAction" }));
        });
        Assert.Equal("PICKTASKS", s2.Single(p => p.PageCode == "PACKINGSTATION").ParentPageCode);

        var actions = s2.SelectMany(p => p.Actions).ToDictionary(a => a.ActionCode, a => (a.PermissionKey, a.IsDangerous));
        Assert.Equal(new Dictionary<string, (string, bool)>
        {
            ["OUTBOUND_CREATE"] = (WarehousePermissions.OutboundShipmentsCreate, false),
            ["OUTBOUND_UPDATE"] = (WarehousePermissions.OutboundShipmentsUpdate, false),
            ["OUTBOUND_PACK"] = (WarehousePermissions.PackagesCreate, false),
            ["OUTBOUND_RELEASE"] = (WarehousePermissions.OutboundShipmentsRelease, false),
            ["OUTBOUND_SYNC_DISPATCH"] = (WarehousePermissions.OutboundShipmentsUpdate, false),
            ["OUTBOUND_CANCEL"] = (WarehousePermissions.OutboundShipmentsCancel, true),
            ["PICK_TASK_CONFIRM"] = (WarehousePermissions.PickTasksConfirm, false),
            ["PICK_TASK_RETRY"] = (WarehousePermissions.PickTasksConfirm, false),
            ["PICK_TASK_RETURNED"] = (WarehousePermissions.PickTasksConfirm, false),
            ["PICK_TASK_CLOSE_SHORT"] = (WarehousePermissions.PickTasksConfirm, false),
            ["PICK_TASK_CLAIM"] = (WarehousePermissions.PickTasksConfirm, false),
            ["PICK_TASK_ASSIGN"] = (WarehousePermissions.PickTasksUpdate, false),
            ["PICK_TASK_RESOLVE_INTEGRITY"] = (WarehousePermissions.PickTasksResolve, true),
            ["PACKAGE_CREATE"] = (WarehousePermissions.PackagesCreate, false),
            ["PACKAGE_UPDATE"] = (WarehousePermissions.PackagesUpdate, false),
            ["PACKAGE_CLOSE"] = (WarehousePermissions.PackagesUpdate, false)
        }, actions);
    }

    [Fact]
    public void The_reads_need_read_and_every_write_needs_manage()
    {
        var actions = typeof(WarehouseLocationRolesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToDictionary(m => m.GetCustomAttribute<HttpMethodAttribute>()!.HttpMethods.Single() + " " + m.GetCustomAttribute<HttpMethodAttribute>()!.Template,
                m => m.GetCustomAttribute<HasPermissionAttribute>()!.Policy!.Replace("Permission:", string.Empty));

        Assert.Equal(new Dictionary<string, string?>
        {
            ["GET "] = WarehousePermissions.LocationRolesRead,
            ["GET {roleId}"] = WarehousePermissions.LocationRolesRead,
            ["POST "] = WarehousePermissions.LocationRolesManage,
            ["PUT {roleId}"] = WarehousePermissions.LocationRolesManage,
            ["DELETE {roleId}"] = WarehousePermissions.LocationRolesManage,
            ["POST bulk-delete"] = WarehousePermissions.LocationRolesManage
        }, actions.ToDictionary(a => a.Key, a => (string?)a.Value));
        Assert.Equal("api/warehouse/location-roles", typeof(WarehouseLocationRolesController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public void The_warehouse_base_path_is_scoped_by_the_middleware()
        => Assert.Contains(WarehouseContract.BasePath, InventoryScopeResolutionMiddleware.ScopedBasePaths);

    [Fact]
    public void The_warehouse_code_names_no_ledger_store_no_stock_total_and_no_sales_issue()
    {
        var warehouse = SourceScan.Production()
            .Where(f => Regex.IsMatch(f.RelativePath, @"/(Warehouse|WarehouseLocationRoles|PutawayTasks|OutboundShipments|PickTasks|HandlingUnits)/")) // DL-4 / DL-5: the S2 folders too
            .ToList();
        Assert.True(warehouse.Count >= 15, $"vacuous scan: {warehouse.Count} files");

        // DL-5 (AC-31, W-4, sabotage (t)): 0178 never writes the sales issue — neither the wire name nor the movement enum member
        foreach (var forbidden in new[] { "IInventoryLedgerRepository", "inventory_transactions", "inventory_balances", "GOODS_ISSUE_SALES", "GoodsIssueSales" })
        {
            Assert.DoesNotContain(warehouse, f => f.Body.Contains(forbidden, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(warehouse, f => Regex.IsMatch(f.Body, @"\b(OnHand|Available|Reserved)\s*\{\s*get;"));
    }
}
