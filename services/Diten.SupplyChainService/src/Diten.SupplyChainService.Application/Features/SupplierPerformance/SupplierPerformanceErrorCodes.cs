namespace Diten.SupplyChainService.Application.Features.SupplierPerformance;
/// <summary>
/// Every error code this module emits: the SUPPLIER-PERFORMANCE v1 codes (supplier-performance.openapi.yaml) plus the codes
/// MOD-0147 §0.0 "Undeclared statuses" and §0.5 declare for the contract successor 1.1.0. A test compares this list with the
/// contract and the pack; the shipment-bundle list (ContractErrorCodes) is a different contract and stays apart.
/// </summary>
public static class SupplierPerformanceErrorCodes
{
    // Declared by the frozen v1 contract.
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string UnknownSupplier = "UNKNOWN_SUPPLIER";
    public const string UnknownEvaluation = "UNKNOWN_EVALUATION";
    public const string UnknownScorecard = "UNKNOWN_SCORECARD";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string VersionConflict = "VERSION_CONFLICT";
    public const string SupplierBaseUnavailable = "SUPPLIER_BASE_UNAVAILABLE";
    // Declared by the pack (§0.0 undeclared statuses, §0.5), carried by the contract successor 1.1.0.
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string DuplicateEvaluation = "DUPLICATE_EVALUATION";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string PersistenceUnavailable = "PERSISTENCE_UNAVAILABLE";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
    // error.details.code of a 422 (§0.0 D2, review F-2).
    public const string SupplierCodeNotCanonical = "SUPPLIER_CODE_NOT_CANONICAL";
}
