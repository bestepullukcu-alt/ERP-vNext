namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
// Frozen contract enum. v1 creates DRAFT only; PUBLISHED comes with submit (DL-2); no operation produces CANCELLED (pack §0.2).
public enum EvaluationStatus { DRAFT, PUBLISHED, CANCELLED }
