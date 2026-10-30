using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Common;
public sealed class RequestContext
{
    public ShipmentScope Scope { get; set; } = new(Guid.Empty, Guid.Empty, Guid.Empty);
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
    /// <summary>The caller's Authorization header, forwarded to INVENTORY by the dispatch issue (MOD-0183 §23.2).</summary>
    public string Authorization { get; set; } = string.Empty;
}
