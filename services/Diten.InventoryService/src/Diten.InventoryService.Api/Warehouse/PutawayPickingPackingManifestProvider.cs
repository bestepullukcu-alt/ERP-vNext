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
                    ])
            ],
            Icon: "bx-package",
            ApiPermissions:
            [
                // the frozen outbound read (MVP-6 reads it — API-only by design, no S1 screen)
                new ModuleManifestPermission(WarehousePermissions.OutboundShipmentsRead, "Read outbound shipments", "Tenant")
            ]);
}
