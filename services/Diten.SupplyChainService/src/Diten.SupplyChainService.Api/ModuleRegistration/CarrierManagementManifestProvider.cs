using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

public sealed class CarrierManagementManifestProvider : IModuleManifestProvider
{
    private const string Read = "supplychain.carriers.read";
    private const string Create = "supplychain.carriers.create";
    private const string ChangeStatus = "supplychain.carriers.status.change";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "carrier-management",
            ModuleName: "CarrierManagement",
            DisplayName: "Carrier Management",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 400,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "CARRIERS",
                    DisplayName: "Carriers",
                    RoutePath: "/SupplyChain/Carriers",
                    RequiredPermission: Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create", Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CHANGE_STATUS", "Change Status", ChangeStatus, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-truck",
            IsBaseline: false);
}
