using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// MOD-0176 Expiry / FEFO Management self-registration manifest (pack §14.4): module code <c>EXPIRY-FEFO-MANAGEMENT</c>, SortOrder
/// 176, namespace <c>expiry.*</c> ONLY (FU-0174-CAT-01: a foreign key in a manifest moves that key to this module in the catalog).
/// <para><b>WP-0176-F4:</b> the four F4 pages of pack §11.1 (FEFO Paneli, Son Kullanma Yaklaşanlar, Süresi Dolmuş Stok, FEFO Sırası) under
/// <c>/Expiry</c> — none under <c>/Platform</c>, so every key is Tenant scope — with their buttons (§11.3). The keys a page or a button now
/// carries left <c>ApiPermissions</c> (<c>expiry.exceptions.create</c> is the "İstisna aç" button). <b>WP-0176-F5:</b> the pages İstisna
/// Kutusu, İstisna Detayı (nav-hidden, parent the inbox), Ayarlar and Kanıt Paketi with their buttons; only <c>expiry.date-overrides.create</c>
/// stays an API permission (pack §14.4 — the lot screens call it). "Karantinaya al" is a link to MOD-0175 under that module's key
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
                    ]),
                // WP-0176-F5 (pack §11.1, §11.3): İstisna Kutusu, İstisna Detayı (not in the menu — opened from the inbox), Ayarlar, Kanıt Paketi.
                new ModuleManifestPage(
                    PageCode: "EXPIRYEXCEPTIONS",
                    DisplayName: "Exception inbox",
                    RoutePath: "/Expiry/Exceptions",
                    RequiredPermission: ExpiryPermissions.ExceptionsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EXCEPTION_OPEN", "Open exception", ExpiryPermissions.ExceptionsCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "EXPIRYEXCEPTIONDETAILS",
                    DisplayName: "Exception detail",
                    RoutePath: "/Expiry/Exceptions/Details/{id}",
                    RequiredPermission: ExpiryPermissions.ExceptionsRead,
                    ParentPageCode: "EXPIRYEXCEPTIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 55,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EXCEPTION_TAKE", "Take into review", ExpiryPermissions.ExceptionsReview, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EXPIRY_EXCEPTION_COMMENT", "Add comment", ExpiryPermissions.ExceptionsComment, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EXPIRY_EXCEPTION_RESOLVE", "Decide (sign and save)", ExpiryPermissions.ExceptionsResolve, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EXPIRY_EXCEPTION_CLOSE", "Close exception", ExpiryPermissions.ExceptionsClose, "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "EXPIRYPOLICY",
                    DisplayName: "Shelf-life rules and settings",
                    RoutePath: "/Expiry/Policy",
                    RequiredPermission: ExpiryPermissions.PoliciesRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Form",
                    SortOrder: 60,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_POLICY_SAVE", "Save settings", ExpiryPermissions.PoliciesManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "EXPIRYEVIDENCE",
                    DisplayName: "Evidence pack",
                    RoutePath: "/Expiry/Evidence",
                    RequiredPermission: ExpiryPermissions.EvidenceExport,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 70,
                    Actions:
                    [
                        new ModuleManifestAction("EXPIRY_EVIDENCE_EXPORT", "Download evidence (CSV / PDF)", ExpiryPermissions.EvidenceExport, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            Icon: "bx-time-five",
            ApiPermissions:
            [
                // WP-0176-F2: createDateOverride (KF-D) — API-only by design (pack §14.2): the lot screens call it, it has no page of its own.
                new ModuleManifestPermission(ExpiryPermissions.DateOverridesCreate, "Prepare a lot-date override", "Tenant")
            ]);
}
