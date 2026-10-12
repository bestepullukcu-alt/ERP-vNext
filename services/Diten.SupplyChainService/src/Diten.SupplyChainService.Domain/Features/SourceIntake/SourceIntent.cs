using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Domain.Features.SourceIntake;

// Confidential durable preparation; not a source claim or a public Shipment DTO.
public sealed record SourceIntent(string Key, string Snapshot, string Hash, string MappingVersion,
    Shipment Shipment, string Defaults, Guid CommandId, Guid HistoryId, Guid AuditId, Guid EventId, Guid ReceiptId,
    Guid? SourceCorrelationId, Guid LocalCorrelationId, string CorrelationOrigin, string State = "Pending");
public interface ISourceIntakeStore
{
    Task<SourceIntent?> FindAsync(ShipmentScope scope, string key, CancellationToken ct);
    Task<SourceIntent> PrepareAsync(ShipmentScope scope, SourceIntent candidate, CancellationToken ct);
    Task RecordEvidenceAsync(ShipmentScope scope, string key, string outboundId, string state, string evidence, CancellationToken ct);
}
