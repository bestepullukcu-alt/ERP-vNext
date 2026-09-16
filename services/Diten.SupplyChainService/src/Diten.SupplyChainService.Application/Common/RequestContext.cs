using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Common;
public sealed class RequestContext
{
    public ShipmentScope Scope { get; set; } = new(Guid.Empty, Guid.Empty, Guid.Empty);
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
}
