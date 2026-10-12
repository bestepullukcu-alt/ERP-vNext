using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.ModuleRegistration;

/// <summary>
/// MOD-0175 Quarantine / Blocked Stock self-registration manifest — the pack §14 tables verbatim: module code
/// <c>QUARANTINE-BLOCKED-STOCK</c> (CT-Q12, domain practice), namespace <c>quarantine.*</c> ONLY (FU-0174-CAT-01: a foreign key
/// in a manifest moves that key to this module in the catalog), eight pages under <c>/Quarantine</c> — none under
/// <c>/Platform</c>, so every key is Tenant scope — and every mockup button with its real key. No <c>ApiPermissions</c>:
/// every key has a page or a button (pack §14). The screens are drawn by slice Q5; the manifest is registered from Q1
/// (MOD-0174 P1 pattern). Links to MOD-0174 lot pages are not actions of this manifest.
/// </summary>
public sealed class QuarantineBlockedStockManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "QUARANTINE-BLOCKED-STOCK";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Quarantine / Blocked Stock",
            DisplayName: "Quality",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 175,
            Icon: "bx-shield-quarter",
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "QUARANTINEQUEUE",
                    DisplayName: "Quarantine queue",
                    RoutePath: "/Quarantine",
                    RequiredPermission: QuarantinePermissions.HoldsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("DECIDE", "Decide", QuarantinePermissions.DecisionsCreate, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEWHOLD", "Detail", QuarantinePermissions.HoldsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "QUARANTINEDETAILS",
                    DisplayName: "Quarantine record",
                    RoutePath: "/Quarantine/Details",
                    RequiredPermission: QuarantinePermissions.HoldsRead,
                    ParentPageCode: "QUARANTINEQUEUE",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("DECIDE", "Decide", QuarantinePermissions.DecisionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("STARTREVIEW", "Start review", QuarantinePermissions.HoldsReview, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("REFRESHWORKFLOW", "Refresh status", QuarantinePermissions.HoldsRead, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("WITHDRAWDECISION", "Withdraw decision", QuarantinePermissions.DecisionsWithdraw, "Toolbar", 40, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("REAPPLYDECISION", "Re-apply", QuarantinePermissions.DecisionsApply, "Toolbar", 50, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "QUARANTINEDECIDE",
                    DisplayName: "Decide",
                    RoutePath: "/Quarantine/Decide",
                    RequiredPermission: QuarantinePermissions.DecisionsCreate,
                    ParentPageCode: "QUARANTINEQUEUE",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 12,
                    Actions:
                    [
                        new ModuleManifestAction("SUBMITDECISION", "Sign and send", QuarantinePermissions.DecisionsCreate, "Form", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "DECISIONSUBMIT",
                    DisplayName: "Submit decision",
                    RoutePath: "/Quarantine/Decisions/Submit",
                    RequiredPermission: QuarantinePermissions.DecisionsCreate,
                    ParentPageCode: "QUARANTINEQUEUE",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 13,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "BLOCKEDSTOCK",
                    DisplayName: "Blocked stock",
                    RoutePath: "/Quarantine/Blocked",
                    RequiredPermission: QuarantinePermissions.HoldsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("DISPOSE", "Disposition", QuarantinePermissions.BlockedDispose, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("UNBLOCK", "Unblock", QuarantinePermissions.BlockedUnblock, "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COMPLETEREVIEW", "Complete review", QuarantinePermissions.HoldsReview, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("BLOCKSTOCK", "Block stock", QuarantinePermissions.BlockedBlock, "Toolbar", 40, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "BLOCKEDDISPOSITION",
                    DisplayName: "Disposition",
                    RoutePath: "/Quarantine/Blocked/Disposition",
                    RequiredPermission: QuarantinePermissions.BlockedDispose,
                    ParentPageCode: "BLOCKEDSTOCK",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 21,
                    Actions:
                    [
                        new ModuleManifestAction("SUBMITDISPOSITION", "Sign and send", QuarantinePermissions.BlockedDispose, "Form", 10, IsDangerous: true, IsToolbarAction: false, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "RETESTQUEUE",
                    DisplayName: "Retest queue",
                    RoutePath: "/Quarantine/Retest",
                    RequiredPermission: QuarantinePermissions.HoldsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("QUARANTINELOT", "Quarantine", QuarantinePermissions.HoldsCreate, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("VIEWHOLD", "Open record", QuarantinePermissions.HoldsRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "QUARANTINESETTINGS",
                    DisplayName: "Quarantine settings",
                    RoutePath: "/Quarantine/Settings",
                    RequiredPermission: QuarantinePermissions.SettingsManage,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Form",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("SAVESETTINGS", "Save", QuarantinePermissions.SettingsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PREPARETEMPLATE", "Prepare approval template", QuarantinePermissions.SettingsManage, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            ApiPermissions: []);
}
