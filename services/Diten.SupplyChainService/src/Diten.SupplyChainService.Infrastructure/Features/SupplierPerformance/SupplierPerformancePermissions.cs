namespace Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
// MOD-0147 §14 (v1 mapping). Declared for the CI guard by a KnownWithoutProvider entry; the provider and the permission
// definitions ship with the enabling change (§0.0), never in DL-1. .publish (submit) comes with DL-2.
public static class SupplierPerformancePermissions
{
    public const string Read = "supplychain.supplier-performance.read";
    public const string Evaluate = "supplychain.supplier-performance.evaluate";
}
