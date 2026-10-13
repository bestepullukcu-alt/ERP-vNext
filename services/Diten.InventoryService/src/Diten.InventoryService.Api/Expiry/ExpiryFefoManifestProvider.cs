using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// MOD-0176 Expiry / FEFO Management self-registration manifest (pack §14.4): module code <c>EXPIRY-FEFO-MANAGEMENT</c>, SortOrder
/// 176, namespace <c>expiry.*</c> ONLY (FU-0174-CAT-01: a foreign key in a manifest moves that key to this module in the catalog).
/// <para><b>WP-0176-F4:</b> the four F4 pages of pack §11.1 (FEFO Paneli, Son Kullanma Yaklaşanlar, Süresi Dolmuş Stok, FEFO Sırası) under
/// <c>/Expiry</c> — none under <c>/Platform</c>, so every key is Tenant scope — with their buttons (§11.3). The keys a page or a button now
/// carries left <c>ApiPermissions</c> (<c>expiry.exceptions.create</c> is the "İstisna aç" button); the F5 keys stay API permissions
/// until their pages (Exceptions, Details, Policy, Evidence) come. "Karantinaya al" is a link to MOD-0175 under that module's key
/// (<c>quarantine.holds.create</c>) and is not an action of this manifest.</para>
/// </summary>
public sealed class ExpiryFefoManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "EXPIRY-FEFO-MANAGEMENT";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Expiry / FEFO Management",
            DisplayName: "Expiry / FEFO",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 176,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "EXPIRYDASHBOARD",
                    DisplayName: "FEFO dashboard",
                    RoutePath: "/Expiry",
                    RequiredPermission: ExpiryPermissions.DashboardRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EXCEPTION_OPEN", "Open exception", ExpiryPermissions.ExceptionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "EXPIRYAPPROACHING",
                    DisplayName: "Approaching expiry",
                    RoutePath: "/Expiry/Approaching",
                    RequiredPermission: ExpiryPermissions.DashboardRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EXCEPTION_OPEN", "Open exception", ExpiryPermissions.ExceptionsCreate, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EXPIRY_FEFO_ORDER_VIEW", "View FEFO order", ExpiryPermissions.FefoOrderRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EXPIRY_LIST_EXPORT", "Export (CSV)", ExpiryPermissions.DashboardRead, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "EXPIREDSTOCK",
                    DisplayName: "Expired stock",
                    RoutePath: "/Expiry/Expired",
                    RequiredPermission: ExpiryPermissions.DashboardRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EXCEPTION_OPEN", "Open exception", ExpiryPermissions.ExceptionsCreate, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EXPIRY_FEFO_ORDER_VIEW", "View FEFO order", ExpiryPermissions.FefoOrderRead, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("EXPIRY_LIST_EXPORT", "Export (CSV)", ExpiryPermissions.DashboardRead, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "FEFOORDER",
                    DisplayName: "FEFO order",
                    RoutePath: "/Expiry/FefoOrder",
                    RequiredPermission: ExpiryPermissions.FefoOrderRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_FEFO_ORDER_CALCULATE", "Calculate order", ExpiryPermissions.FefoOrderRead, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            Icon: "bx-time-five",
            ApiPermissions:
            [
                new ModuleManifestPermission(ExpiryPermissions.PoliciesRead, "Read the expiry policy", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.PoliciesManage, "Change the expiry policy", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsRead, "Read FEFO exceptions", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsComment, "Comment on a FEFO exception", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsReview, "Take a FEFO exception into review", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsResolve, "Resolve a FEFO exception", "Tenant"),
                new ModuleManifestPermission(ExpiryPermissions.ExceptionsClose, "Close a FEFO exception", "Tenant"),
                // WP-0176-F2: createDateOverride (KF-D) — API-only by design (pack §14.2): the lot screens call it, it has no page of its own.
                new ModuleManifestPermission(ExpiryPermissions.DateOverridesCreate, "Prepare a lot-date override", "Tenant")
            ]);
}
