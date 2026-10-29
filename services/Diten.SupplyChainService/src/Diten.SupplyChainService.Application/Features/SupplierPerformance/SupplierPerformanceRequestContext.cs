using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance;
/// <summary>Written only by SupplierPerformanceContextMiddleware after §0.5 stages 1–3b passed.</summary>
public sealed class SupplierPerformanceRequestContext
{
    public SupplierPerformanceScope Scope { get; set; } = new(Guid.Empty, Guid.Empty, Guid.Empty);
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    /// <summary>The caller's token, forwarded to Procurement's eligibility read (pack §0.0 D2: the caller's token).</summary>
    public string Authorization { get; set; } = "";
}
