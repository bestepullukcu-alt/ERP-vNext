using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.ModuleRegistration;

/// <summary>
/// MOD-0174 Lot/Batch/Serial Tracking self-registration manifest — a SEPARATE module from INVENTORY-LEDGER (pack §14,
/// CT-D5): module code <c>LOT-SERIAL-TRACKING</c>, permission namespace <c>trace.*</c>, own nav group "Lot / Seri Takibi"
/// (approved mockup). It is the pack §14 tables verbatim: eight pages (four nav-visible: Lot Gezgini, Seri Gezgini,
/// İzlenebilirlik, Elle girilmiş lotlar) and every button of the mockup as an action with its real key. The screens are
/// drawn by slice P3; the manifest is registered from P1 because the pack fixes it there (WP-0174-P1).
/// <para>
/// <c>trace.genealogy.link</c> has no screen (MOD-0194 calls linkGenealogy) → <c>ApiPermissions</c>
/// (module-self-registration-standard §2d). The cross-module action <c>NEWLOTQUICK</c> (Stock Ledger form) belongs to
/// the INVENTORY-LEDGER manifest and is added by slice P3. Routes are tenant routes, so every key is scoped Tenant (§2c).
/// </para>
/// <para>
/// FU-0174-CAT-01 (WP-0174-P2): every key here is in the module's own namespace <c>trace.*</c>. The actions that open a
/// Stock Ledger screen (<c>OPENINLEDGER</c>, <c>VIEWRECEIPT</c>, <c>RECEIPTINLEDGER</c>) carry <c>trace.lots.read</c>: the
/// target page enforces its own <c>inventory.stock.read</c>. Declaring a MOD-0173 key here made the catalog sync attribute
/// it to this module (the last manifest that syncs a key sets its Module).
/// </para>
/// </summary>
public sealed class LotSerialTrackingManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "LOT-SERIAL-TRACKING";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Lot/Batch/Serial Tracking",
            DisplayName: "Lot / Serial Tracking",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 174,
            Icon: "bx-barcode",
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "LOTS",
                    DisplayName: "Lot Explorer",
                    RoutePath: "/Lots",
                    RequiredPermission: TracePermissions.LotsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("NEWLOT", "New lot", TracePermissions.LotsCreate, "Toolbar", 5, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("VIEWLOT", "Lot detail", TracePermissions.LotsRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDITLOT", "Edit", TracePermissions.LotsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDITRELEASEDLOT", "Edit", TracePermissions.LotsUpdateReleased, "RowAction", 21, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGELOTSTATUS", "Change status", TracePermissions.LotsChangeStatus, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOTMOVES", "Movement history", TracePermissions.LotsRead, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOTTRACEOPEN", "Traceability", TracePermissions.LotsRead, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOTDETAILS",
                    DisplayName: "Lot detail",
                    RoutePath: "/Lots/Details",
                    RequiredPermission: TracePermissions.LotsRead,
                    ParentPageCode: "LOTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("EDITLOT", "Edit", TracePermissions.LotsUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EDITRELEASEDLOT", "Edit", TracePermissions.LotsUpdateReleased, "RowAction", 21, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGELOTSTATUS", "Change status", TracePermissions.LotsChangeStatus, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOTMOVES", "Movement history", TracePermissions.LotsRead, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LOTTRACEOPEN", "Traceability", TracePermissions.LotsRead, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("OPENINLEDGER", "Open in Stock Ledger", TracePermissions.LotsRead, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEWALLSERIALS", "All serials", TracePermissions.SerialsRead, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOTCREATE",
                    DisplayName: "New lot",
                    RoutePath: "/Lots/Create",
                    RequiredPermission: TracePermissions.LotsCreate,
                    ParentPageCode: "LOTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "LOTEDIT",
                    DisplayName: "Edit lot",
                    RoutePath: "/Lots/Edit",
                    RequiredPermission: TracePermissions.LotsUpdate,
                    ParentPageCode: "LOTS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "LOTMOVEMENTS",
                    DisplayName: "Lot movement history",
                    RoutePath: "/LotMovements",
                    RequiredPermission: TracePermissions.LotsRead,
                    ParentPageCode: "LOTS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 14,
                    Actions:
                    [
                        new ModuleManifestAction("VIEWRECEIPT", "Movement receipt", TracePermissions.LotsRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("RECEIPTINLEDGER", "Open in Stock Ledger", TracePermissions.LotsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "SERIALS",
                    DisplayName: "Serial Explorer",
                    RoutePath: "/Serials",
                    RequiredPermission: TracePermissions.SerialsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("SERIALLOT", "Lot detail", TracePermissions.LotsRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("SERIALMOVES", "Movement history", TracePermissions.LotsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOTTRACE",
                    DisplayName: "Traceability",
                    RoutePath: "/LotTrace",
                    RequiredPermission: TracePermissions.LotsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Detail",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("TRACELOTDETAIL", "Lot detail", TracePermissions.LotsRead, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "LOTMIGRATION",
                    DisplayName: "Typed lot migration",
                    RoutePath: "/LotMigration",
                    RequiredPermission: TracePermissions.LotsMigrate,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("APPLYALLSUGGESTIONS", "Apply all suggestions", TracePermissions.LotsMigrate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CONFIRMMIGRATION", "Confirm migration", TracePermissions.LotsMigrate, "Toolbar", 20, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("APPLYSUGGESTION", "Apply suggestion", TracePermissions.LotsMigrate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("LINKEXISTINGLOT", "Link to existing lot", TracePermissions.LotsMigrate, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("NEWLOTFROMTEXT", "Create as new lot", TracePermissions.LotsMigrate, "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("MARKTYPO", "Typo — link to another lot", TracePermissions.LotsMigrate, "RowAction", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CHANGEDECISION", "Change", TracePermissions.LotsMigrate, "RowAction", 70, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            ApiPermissions:
            [
                new ModuleManifestPermission(TracePermissions.GenealogyLink, "Link lot genealogy", "Tenant")
            ]);
}
