namespace Diten.SupplyChainService.Application.Features.SupplierPerformance;
/// <summary>
/// MOD-0147 §0.0 D2: GET /api/suppliers/{supplierId}/eligibility (SUPPLIER 1.3.0) with the caller's token and the validated
/// legal entity. Returns the canonical supplier code of the answer; every other outcome is a SupplierPerformanceFailure:
/// 404 UNKNOWN_SUPPLIER, 403 FORBIDDEN or 503 SUPPLIER_BASE_UNAVAILABLE.
/// </summary>
public interface ISupplierEligibilityReader
{
    Task<string> CanonicalSupplierIdAsync(string requestedSupplierId, CancellationToken ct);
}
