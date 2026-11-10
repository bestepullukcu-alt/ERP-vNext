using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.ProcurementService.Api.ModuleRegistration;

/// <summary>
/// MOD-0140 Supplier Onboarding self-registration manifest. FAZ 1 scaffold: nav-visible Suppliers list page + the
/// verbatim <c>procurement.suppliers.*</c> permission keys the API enforces via [HasPermission]. The read page is the
/// only enforced surface today (GET list + by-id); create/update/onboarding actions arrive in FAZ 2 and are appended
/// then so the manifest stays zero-drift to the code.
/// </summary>
public sealed class SupplierOnboardingManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "procurement-suppliers",
            ModuleName: "Supplier Onboarding",
            DisplayName: "Suppliers",
            Domain: "Procurement",
            Service: "Procurement",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 140,
            Icon: "bx-store",
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "SUPPLIERS",
                    DisplayName: "Suppliers",
                    RoutePath: "/Suppliers",
                    RequiredPermission: "procurement.suppliers.read",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: [])
            ],
            // MOD-0140 DL-3 (§5, §14 "Kataloğa giriş"; Revizyon 8 F-1; K-2): keys of endpoints without a screen enter the Auth
            // catalogue from here — eligibility.read (the three eligibility endpoints, MVP-6 / Treasury roles) and master.update
            // (the common fields, K-1; ApiPermissions so module sync does not hand it to Admin — K-2). read-tenant-wide is NOT
            // declared: it belongs to DL-3's deferred part, after Auth's ExplicitGrantOnlyPermissions is on main and deployed
            // first (§7, §18). create/update/delete/bulk-delete/onboard become page actions with their screens in DL-5 (§20).
            ApiPermissions:
            [
                new ModuleManifestPermission("procurement.suppliers.eligibility.read", "Supplier eligibility (read)", Scope: "Tenant"),
                new ModuleManifestPermission("procurement.suppliers.master.update", "Supplier common fields (update)", Scope: "Tenant")
            ]);
}
