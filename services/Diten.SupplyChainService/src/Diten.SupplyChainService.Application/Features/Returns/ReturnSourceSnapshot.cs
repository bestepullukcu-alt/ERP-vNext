using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
/// <summary>Immutable observed source values, never a Shipment master or a distributed lock.</summary>
public sealed record ReturnSourceSnapshot(Guid ShipmentId,string Status,Guid Root,DateTimeOffset ObservedAt,IReadOnlyList<ReturnSourceLine> Lines)
{ public ReturnReferenceSnapshot ToDomain()=>new(ShipmentId,Status,Root,ObservedAt,Lines.ToArray()); }
