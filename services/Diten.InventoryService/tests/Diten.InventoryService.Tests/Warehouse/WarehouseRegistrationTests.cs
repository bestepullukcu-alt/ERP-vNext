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
        Assert.Empty(manifest.Pages); // DL-1 has no screen (DL-3)

        var keys = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();
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
