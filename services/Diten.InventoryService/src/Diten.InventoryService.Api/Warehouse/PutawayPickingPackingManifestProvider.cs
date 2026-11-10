using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Warehouse;

/// <summary>
/// MOD-0178 Putaway / Picking / Packing self-registration manifest (pack §14.4): module code <c>PUTAWAY-PICKING-PACKING</c> (= the central
/// audit records' source module, <see cref="WarehouseCentralAudit.Module"/>), SortOrder 178, namespace <c>warehouse.*</c> ONLY (FU-0174-CAT-01).
/// <para><b>DL-1 / DL-2</b> had no page (the 0176 F1 / 0177 R1 "sayfasız ilk dilim" pattern): the enforced keys were <c>ApiPermissions</c>.</para>
/// <para><b>DL-3:</b> the two S1 screens (pack §11.1) — <c>PUTAWAYTASKS</c> and <c>WAREHOUSELOCATIONROLES</c>, Scope Tenant, never under
/// <c>/Platform/</c> — with one action per real §11.3 button; their keys leave <c>ApiPermissions</c>. Only the frozen outbound read stays an API
/// permission (MVP-6 reads it; no S1 screen — pack §14.4). The "Uygula" suggestion button is a form control, not an action (§11.3).</para>
/// <para><b>DL-4 / DL-5:</b> the S2 keys were API permissions until their screens existed.</para>
/// <para><b>DL-6:</b> the three S2 screens (pack §11.1, §14.4) — <c>OUTBOUNDSHIPMENTS</c> (nav; Compact: list + Create / Edit / Details),
/// <c>PICKTASKS</c> (nav) and <c>PACKINGSTATION</c> (not in the menu; reached from a pick task or an outbound — its page key is
/// <c>warehouse.packages.read</c>, never the outbound read: the operator packs without seeing the ship-to, Q-10a / AC-34) — with one action per
/// real §11.3 S2 button. Every S2 key is now a page or action key, so nothing is left API-only: the frozen outbound read MVP-6 uses is the
/// OUTBOUNDSHIPMENTS page key (same key, same Tenant scope). "Paketle" on a pick task is a link (navigation), not an action (§11.3).</para>
/// <para><b>DL-7:</b> the three S3 monitoring screens (pack §11.1, §14.4) — <c>WAREHOUSEEXCEPTIONS</c>, <c>WAREHOUSEACTIVITY</c> (Slim lists) and
/// <c>WAREHOUSEINTEGRATION</c> (four cards, not a DataTable — r23), all in the menu, each with its own read key and NO action (§14.4 "S3'te eylem
/// yok").</para>
/// </summary>
public sealed class PutawayPickingPackingManifestProvider : IModuleManifestProvider
{
    // a literal, not WarehouseCentralAudit.Module: the Web nav guard reads the manifest SOURCE (NavManifestL10nGuardTests) and only resolves literal
    // constants; WarehouseRegistrationTests holds the two equal
    public const string ModuleCode = "PUTAWAY-PICKING-PACKING";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Putaway / Picking / Packing",
            DisplayName: "Warehouse",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 178,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "PUTAWAYTASKS",
                    DisplayName: "Putaway tasks",
                    RoutePath: "/Warehouse/PutawayTasks",
                    RequiredPermission: WarehousePermissions.PutawayTasksRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("PUTAWAY_TASK_CREATE", "New putaway task", WarehousePermissions.PutawayTasksCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUTAWAY_TASK_UPDATE", "Edit putaway task", WarehousePermissions.PutawayTasksUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_ASSIGN", "Assign putaway tasks", WarehousePermissions.PutawayTasksUpdate, "BulkAction", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUTAWAY_TASK_CONFIRM", "Confirm putaway", WarehousePermissions.PutawayTasksConfirm, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_RETRY", "Retry a pending confirmation", WarehousePermissions.PutawayTasksConfirm, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_RETURNED", "Mark a refused confirmation as moved back", WarehousePermissions.PutawayTasksConfirm, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_RESOLVE_INTEGRITY", "Resolve an integrity error", WarehousePermissions.PutawayTasksResolve, "RowAction", 70, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_CLOSE_SHORT", "Close the remainder", WarehousePermissions.PutawayTasksConfirm, "RowAction", 80, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_CANCEL", "Cancel putaway task", WarehousePermissions.PutawayTasksCancel, "RowAction", 90, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PUTAWAY_TASK_CLAIM", "Claim putaway task", WarehousePermissions.PutawayTasksConfirm, "RowAction", 100, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "WAREHOUSELOCATIONROLES",
                    DisplayName: "Warehouse location roles",
                    RoutePath: "/Warehouse/LocationRoles",
                    RequiredPermission: WarehousePermissions.LocationRolesRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("LOCATION_ROLE_CREATE", "New location role", WarehousePermissions.LocationRolesManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("LOCATION_ROLE_UPDATE", "Edit location role", WarehousePermissions.LocationRolesManage, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOCATION_ROLE_DELETE", "Delete location role", WarehousePermissions.LocationRolesManage, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOCATION_ROLE_BULK_DELETE", "Delete selected location roles", WarehousePermissions.LocationRolesManage, "BulkAction", 40, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),
                // DL-6 — S2 (pack §11.1 / §11.3)
                new ModuleManifestPage(
                    PageCode: "OUTBOUNDSHIPMENTS",
                    DisplayName: "Outbound shipments",
                    RoutePath: "/Warehouse/OutboundShipments",
                    RequiredPermission: WarehousePermissions.OutboundShipmentsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("OUTBOUND_CREATE", "New outbound shipment", WarehousePermissions.OutboundShipmentsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("OUTBOUND_UPDATE", "Edit outbound shipment", WarehousePermissions.OutboundShipmentsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("OUTBOUND_PACK", "Pack", WarehousePermissions.PackagesCreate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("OUTBOUND_RELEASE", "Ready to ship", WarehousePermissions.OutboundShipmentsRelease, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("OUTBOUND_SYNC_DISPATCH", "Check the shipment", WarehousePermissions.OutboundShipmentsUpdate, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("OUTBOUND_CANCEL", "Cancel outbound shipment", WarehousePermissions.OutboundShipmentsCancel, "RowAction", 60, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "PICKTASKS",
                    DisplayName: "Pick tasks",
                    RoutePath: "/Warehouse/PickTasks",
                    RequiredPermission: WarehousePermissions.PickTasksRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("PICK_TASK_CONFIRM", "Confirm pick", WarehousePermissions.PickTasksConfirm, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PICK_TASK_RETRY", "Retry a pending confirmation", WarehousePermissions.PickTasksConfirm, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PICK_TASK_RETURNED", "Mark a refused confirmation as moved back", WarehousePermissions.PickTasksConfirm, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PICK_TASK_CLOSE_SHORT", "Close the remainder", WarehousePermissions.PickTasksConfirm, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PICK_TASK_CLAIM", "Claim pick task", WarehousePermissions.PickTasksConfirm, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PICK_TASK_ASSIGN", "Assign pick tasks", WarehousePermissions.PickTasksUpdate, "BulkAction", 60, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PICK_TASK_RESOLVE_INTEGRITY", "Resolve an integrity error", WarehousePermissions.PickTasksResolve, "RowAction", 70, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "PACKINGSTATION",
                    DisplayName: "Packing",
                    RoutePath: "/Warehouse/Packing/{outboundId}",
                    RequiredPermission: WarehousePermissions.PackagesRead,
                    ParentPageCode: "PICKTASKS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("PACKAGE_CREATE", "New package", WarehousePermissions.PackagesCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PACKAGE_UPDATE", "Edit package", WarehousePermissions.PackagesUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("PACKAGE_CLOSE", "Close package", WarehousePermissions.PackagesUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                // DL-7 — S3 monitoring (pack §11.1 / §14.4): read-only, no action (S3 has no button — "Kabul et / Çözüldü" is FU-0178-S3-ACT)
                new ModuleManifestPage(
                    PageCode: "WAREHOUSEEXCEPTIONS",
                    DisplayName: "Warehouse exceptions",
                    RoutePath: "/Warehouse/Exceptions",
                    RequiredPermission: WarehousePermissions.ExceptionsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 60,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "WAREHOUSEACTIVITY",
                    DisplayName: "Warehouse activity",
                    RoutePath: "/Warehouse/Activity",
                    RequiredPermission: WarehousePermissions.ActivityRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 70,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "WAREHOUSEINTEGRATION",
                    DisplayName: "Warehouse integration",
                    RoutePath: "/Warehouse/Integration",
                    RequiredPermission: WarehousePermissions.IntegrationMonitorRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Dashboard",
                    SortOrder: 80,
                    Actions: [])
            ],
            Icon: "bx-package",
            // DL-6: every key is a page or an action key now (the frozen outbound read is the OUTBOUNDSHIPMENTS page key) — none is API-only
            ApiPermissions: []);
}
