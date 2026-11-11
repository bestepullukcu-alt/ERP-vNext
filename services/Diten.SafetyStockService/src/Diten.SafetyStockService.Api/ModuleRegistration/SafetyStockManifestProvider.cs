using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.SafetyStockService.Api.ModuleRegistration;

public interface IModuleManifestProvider
{
    ModuleManifestDocument GetManifest();
}

public sealed class SafetyStockManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() => new(
        ModuleCode: "safety-stock-optimization",
        ModuleName: "SafetyStockOptimization",
        DisplayName: "Safety Stock Optimization",
        Domain: "Supply Chain Execution",
        Service: "SafetyStockService",
        ModuleVersion: "1.0.0",
        IsTenantAssignable: true,
        SortOrder: 191,
        Pages: [],
        Icon: "bx-shield-quarter",
        ApiPermissions:
        [
            new("safety-stock.policies.read", "Read safety stock policies", "Tenant"),
            new("safety-stock.evaluations.read", "Read safety stock evaluations", "Tenant"),
            new("safety-stock.policies.create", "Create safety stock policy drafts", "Tenant"),
            new("safety-stock.policies.update", "Create safety stock policy revisions", "Tenant"),
            new("safety-stock.policies.submit", "Submit safety stock policies", "Tenant"),
            new("safety-stock.policies.review", "Review safety stock policies", "Tenant")
        ]);
}
