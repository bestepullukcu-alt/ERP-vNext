using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

/// <summary>
/// MOD-0192 Capacity Planning self-registration manifest (pack §24; DCP-009 §21). DRAFT overlay — not built.
/// Mirrors the Capacity tenant UI (pack §23.4): the entry page at /SupplyChain/CapacityPlans (navigation-visible; "List"
/// page type without a table, F192-LIST) with the Create toolbar action, and the plan workspace detail page with the
/// Create Scenario and Evaluate toolbar actions. Permission keys are the existing CapacityPermissions constants
/// (CapacityPermissions.cs:4-7 in the accepted isolated source BC-SOURCE ebd5d80c…7064); no new key, page or ID.
/// Ships with the Capacity UI only (SR-D4 / DCP-009 §21.4): the AddSingleton line and the Nav.Module/Nav.Page keys are
/// applied by the integration owner from _shared-integration/ in the same change.
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
