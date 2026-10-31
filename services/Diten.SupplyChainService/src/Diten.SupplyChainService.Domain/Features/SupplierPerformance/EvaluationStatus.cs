namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
// Frozen contract enum. Create writes DRAFT, submit PUBLISHED; no operation produces CANCELLED (pack §0.2).
public enum EvaluationStatus { DRAFT, PUBLISHED, CANCELLED }
