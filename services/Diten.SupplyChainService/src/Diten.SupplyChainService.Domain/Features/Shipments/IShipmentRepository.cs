namespace Diten.SupplyChainService.Domain.Features.Shipments;
public interface IShipmentRepository
{
    Task<Shipment?> GetAsync(ShipmentScope scope, Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Shipment> Items, long Total)> QueryAsync(ShipmentScope scope, ShipmentStatus? status, string? sourceDocumentId, int page, int pageSize, CancellationToken ct);
    Task<ShipmentMutationResult> MutateAsync(ShipmentScope scope, Guid? shipmentId, string operation, string key, string fingerprint,
     Guid correlationId, Func<Shipment?, ShipmentChange> decide, CancellationToken ct, Diten.SupplyChainService.Domain.Features.SourceIntake.SourceIntent? source = null);
}
