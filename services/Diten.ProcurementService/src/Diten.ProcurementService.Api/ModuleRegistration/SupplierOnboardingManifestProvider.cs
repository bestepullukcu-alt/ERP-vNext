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
                    RoutePath: "/Procurement/Suppliers",
                    RequiredPermission: "procurement.suppliers.read",
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: [])
            ]);
}
