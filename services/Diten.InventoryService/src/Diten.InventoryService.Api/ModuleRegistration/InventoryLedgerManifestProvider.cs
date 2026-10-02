using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.ModuleRegistration;

/// <summary>
/// MOD-0173-FU01 Inventory Ledger UI self-registration manifest (module-self-registration-standard; pack §14).
/// It mirrors the FRONTEND, with zero drift: only the routes and buttons UI-1 actually ships.
/// <list type="bullet">
/// <item><c>/StockLedger</c> (S-1 stock list, nav-visible) with the row action "History";</item>
/// <item><c>/StockMovements</c> (S-2 movement history, reached from S-1) with the row action "Reversal".</item>
/// </list>
/// Create / Details / Edit ("Correct") / StockLedger Create pages and the NEWMOVEMENT / ROWMOVEMENT actions arrive
/// with UI-2, appended then — never declared before their route exists. Every permission is a real
/// <see cref="InventoryPermissions"/> constant that the API enforces with <c>[HasPermission]</c>.
/// Both routes are tenant routes (not <c>/Platform/…</c>), so the derived permission scope is Tenant (§2c).
/// <para>
/// MOD-0173-FU02 (LOC2) appends the location master to the SAME module (FU02 pack §14 decision): <c>/LocationTree</c>
/// (L-1 tree + detail, nav-visible) and <c>/Locations</c> (L-2 list, reached from L-1), with exactly the buttons LOC2
/// renders — New location (toolbar), Edit / Add child / status change (Activate · Deactivate · Archive, one dangerous
/// action) / History / View in stock list on both, Bulk bins on the L-1 detail of a Zone / StorageLocation. The module
/// then has two nav-visible pages (Stock Ledger, Locations).
/// </para>
/// </summary>
public sealed class InventoryLedgerManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "INVENTORY-LEDGER";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Inventory Ledger",
            DisplayName: "Inventory",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 173,
            Icon: "bx-package",
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "STOCKLEDGER",
                    DisplayName: "Stock Ledger",
                    RoutePath: "/StockLedger",
                    RequiredPermission: InventoryPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("ROWHISTORY", "History", InventoryPermissions.Read, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "STOCKMOVEMENTS",
                    DisplayName: "Stock Movements",
                    RoutePath: "/StockMovements",
                    RequiredPermission: InventoryPermissions.Read,
                    ParentPageCode: "STOCKLEDGER",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("REVERSAL", "Reversal", InventoryPermissions.CreateMovements, "RowAction", 10, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOCATIONTREE",
                    DisplayName: "Locations",
                    RoutePath: "/LocationTree",
                    RequiredPermission: InventoryPermissions.LocationsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Tree",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("NEWLOCATION", "New location", InventoryPermissions.LocationsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDITLOCATION", "Edit", InventoryPermissions.LocationsManage, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ADDCHILDLOCATION", "Add child", InventoryPermissions.LocationsManage, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGELOCATIONSTATUS", "Change status", InventoryPermissions.LocationsManage, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("BULKBINS", "Generate bins", InventoryPermissions.LocationsManage, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOCATIONHISTORY", "History", InventoryPermissions.LocationsRead, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEWLOCATIONSTOCK", "View in stock list", InventoryPermissions.Read, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOCATIONS",
                    DisplayName: "Location list",
                    RoutePath: "/Locations",
                    RequiredPermission: InventoryPermissions.LocationsRead,
                    ParentPageCode: "LOCATIONTREE",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("NEWLOCATION", "New location", InventoryPermissions.LocationsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDITLOCATION", "Edit", InventoryPermissions.LocationsManage, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ADDCHILDLOCATION", "Add child", InventoryPermissions.LocationsManage, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGELOCATIONSTATUS", "Change status", InventoryPermissions.LocationsManage, "RowAction", 40, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOCATIONHISTORY", "History", InventoryPermissions.LocationsRead, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEWLOCATIONSTOCK", "View in stock list", InventoryPermissions.Read, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ]);
}
