using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers;
public sealed class CarrierRequestContext
{
    public CarrierScope Scope { get; set; } = new(Guid.Empty, Guid.Empty, Guid.Empty);
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
