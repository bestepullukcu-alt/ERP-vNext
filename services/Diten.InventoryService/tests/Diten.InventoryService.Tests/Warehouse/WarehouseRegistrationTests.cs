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

        Assert.Equal(new[] { ("PUTAWAYTASKS", "/Warehouse/PutawayTasks", WarehousePermissions.PutawayTasksRead), ("WAREHOUSELOCATIONROLES", "/Warehouse/LocationRoles", WarehousePermissions.LocationRolesRead) },
            manifest.Pages.Select(p => (p.PageCode, p.RoutePath, p.RequiredPermission)));
        Assert.All(manifest.Pages, p =>
        {
            Assert.True(p.IsNavigationVisible);
            Assert.Null(p.ParentPageCode);
            Assert.DoesNotContain("/Platform/", p.RoutePath, StringComparison.OrdinalIgnoreCase);
            Assert.All(p.Actions, a => Assert.Contains(a.ActionType, new[] { "Toolbar", "RowAction", "BulkAction" }));
        });

        var actions = manifest.Pages.SelectMany(p => p.Actions).ToDictionary(a => a.ActionCode, a => (a.PermissionKey, a.IsDangerous));
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

        Assert.Equal([WarehousePermissions.OutboundShipmentsRead], (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey));
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
            .Where(f => Regex.IsMatch(f.RelativePath, @"/(Warehouse|WarehouseLocationRoles|PutawayTasks)/"))
            .ToList();
        Assert.True(warehouse.Count >= 15, $"vacuous scan: {warehouse.Count} files");

        foreach (var forbidden in new[] { "IInventoryLedgerRepository", "inventory_transactions", "inventory_balances", "GOODS_ISSUE_SALES" })
        {
            Assert.DoesNotContain(warehouse, f => f.Body.Contains(forbidden, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(warehouse, f => Regex.IsMatch(f.Body, @"\b(OnHand|Available|Reserved)\s*\{\s*get;"));
    }
}
