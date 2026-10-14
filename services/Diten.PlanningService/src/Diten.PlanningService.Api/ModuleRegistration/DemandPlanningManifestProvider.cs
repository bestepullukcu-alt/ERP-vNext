using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.PlanningService.Api.ModuleRegistration;

public sealed class DemandPlanningManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() => new(
        ModuleCode: "demand-planning",
        ModuleName: "DemandPlanning",
        DisplayName: "Demand Planning",
        Domain: "Supply Chain Execution",
        Service: "PlanningService",
        ModuleVersion: "1.0.0",
        IsTenantAssignable: true,
        SortOrder: 188,
        Pages:
        [
            new ModuleManifestPage(
                PageCode: "DEMANDPLANNING",
                DisplayName: "Demand Planning",
                RoutePath: "/SupplyChain/DemandPlanning",
                RequiredPermission: "demand.plans.read",
                ParentPageCode: null,
                IsNavigationVisible: true,
                PageType: "Workspace",
                SortOrder: 10,
                Actions: [])
        ],
        Icon: "bx-line-chart",
        ApiPermissions:
        [
            new("demand.cycles.create", "Create planning cycles"),
            new("demand.history-imports.import", "Import demand history"),
            new("demand.history-imports.review", "Review history imports"),
            new("demand.history-imports.accept", "Accept history rows"),
            new("demand.history.read", "Read accepted history"),
            new("demand.drafts.create", "Create demand drafts"),
            new("demand.drafts.update", "Update demand drafts"),
            new("demand.plans.review", "Review demand plans"),
            new("demand.plans.publish", "Publish demand plans"),
            new("demand.plans.invalidate", "Invalidate demand plans"),
            new("demand.plans.consume", "Consume demand plans"),
            new("demand.audit.read", "Read demand audit")
        ]);
}
