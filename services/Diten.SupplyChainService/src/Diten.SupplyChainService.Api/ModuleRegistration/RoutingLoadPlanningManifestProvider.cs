using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.Loads;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0185 Routing &amp; Load Planning self-registration manifest (pack §29; DCP-009 §21). Built by R-4b with the Loads UI
/// (pack §30), per the ship rule. One List page at /SupplyChain/Loads gated by supplychain.loads.read, with the create
/// toolbar action. supplychain.loads.transition is declared as the CHANGE_STATUS row action although this UI slice has no
/// transition button (ROOT-UI-01): §29 had it API-only, but Platform syncs to Auth only declared keys, so an undeclared key
/// that LoadsController.Transition enforces could never be granted and that endpoint would answer 403 to every caller —
/// the shape R-2 measured for Returns (F-R2-1) and the R-3 guard now rejects.
/// </summary>
public sealed class RoutingLoadPlanningManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "routing-load-planning",
            ModuleName: "RoutingLoadPlanning",
            DisplayName: "Routing & Load Planning",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 410,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "LOADS",
                    DisplayName: "Loads",
                    RoutePath: "/SupplyChain/Loads",
                    RequiredPermission: LoadPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create", LoadPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("CHANGE_STATUS", "Change Load Status", LoadPermissions.Transition, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            Icon: "bx-map-alt",
            IsBaseline: false);
}
