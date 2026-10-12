namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentChange(Shipment? Shipment, string? ErrorCode, int StatusCode,
 DateTimeOffset OccurredAt, string? ReasonCode = null, string? Note = null)
{
    public static ShipmentChange Fail(string code, int status) => new(null, code, status, default);
}
