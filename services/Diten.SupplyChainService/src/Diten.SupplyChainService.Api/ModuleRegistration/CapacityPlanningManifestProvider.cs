using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0192 Capacity Planning self-registration manifest (pack §24 via the UI-revision patch
/// <c>mvp6-ui-pack-patch-190-192-01</c>; DCP-009 §21). Mirrors the tenant UI (§23, G6): the entry page
/// /SupplyChain/CapacityPlans (Create plan + Open plan by ID — no list, F192-LIST) and the plan workspace
/// /SupplyChain/CapacityPlans/Details/{capacityPlanId:guid} with Create scenario and Evaluate. Keys are the existing
/// CapacityPermissions constants; no new key, page or ID.
/// Ships with the UI in the same change (G4 item C), and is registered only where Capacity is composed (Program.cs, the
/// demand-seed block): a catalog entry on a host whose routes answer 404 would be dead navigation.
/// </summary>
public sealed class CapacityPlanningManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "capacity-planning",
            ModuleName: "CapacityPlanning",
            DisplayName: "Capacity Planning",
            Domain: "SupplyChainExecution",
            Service: "DitenSupplyChainService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 450,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "CAPACITY_PLANS",
                    DisplayName: "Capacity Plans",
                    RoutePath: "/SupplyChain/CapacityPlans",
                    RequiredPermission: CapacityPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Plan", CapacityPermissions.Create, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "CAPACITY_PLAN_DETAILS",
                    DisplayName: "Capacity Plan",
                    RoutePath: "/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}",
                    RequiredPermission: CapacityPermissions.Read,
                    ParentPageCode: "CAPACITY_PLANS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_SCENARIO", "Create Scenario", CapacityPermissions.ScenarioCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EVALUATE", "Evaluate", CapacityPermissions.Evaluate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            Icon: "bx-bar-chart-alt-2",
            IsBaseline: false);
}
