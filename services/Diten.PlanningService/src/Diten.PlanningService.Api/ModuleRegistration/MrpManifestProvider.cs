using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.PlanningService.Api.ModuleRegistration;

public sealed class MrpManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() => new(
        ModuleCode: "mrp-replenishment",
        ModuleName: "MrpReplenishment",
        DisplayName: "MRP & Replenishment",
        Domain: "Supply Chain Execution",
        Service: "PlanningService",
        ModuleVersion: "1.0.0",
        IsTenantAssignable: true,
        SortOrder: 189,
        Pages: [],
        Icon: "bx-git-branch",
        ApiPermissions:
        [
            new("mrp.lead-times.create", "Create MRP lead time", "Tenant"),
            new("mrp.lead-times.update", "Revise MRP lead time", "Tenant"),
            new("mrp.lead-times.submit", "Submit MRP lead time", "Tenant"),
            new("mrp.lead-times.review", "Review MRP lead time", "Tenant"),
            new("mrp.runs.read", "Calculate scoped MRP plan", "Tenant")
        ]);
}
