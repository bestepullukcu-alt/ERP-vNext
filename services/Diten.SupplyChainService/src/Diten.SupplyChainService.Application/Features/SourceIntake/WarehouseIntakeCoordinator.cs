using Diten.SupplyChainService.Application.Features.Shipments;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Validators;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.SourceIntake;
namespace Diten.SupplyChainService.Application.Features.SourceIntake;

public sealed record SourceIntakeResult(string State, Shipment? Shipment, bool Replay, string? Error);
public sealed class WarehouseIntakeCoordinator(IWarehouseReadClient warehouse, ISourceIntakeStore store, IShipmentRepository shipments)
{
    public async Task<SourceIntakeResult> IntakeAsync(TrustedSourceContext context, string outboundId,
        string? upstreamCorrelation = null, Guid? envelopeCorrelation = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(outboundId)) throw new ArgumentException("Outbound identity required.", nameof(outboundId));
        Guid? upstream = null;
        if (upstreamCorrelation is not null)
        {
            if (!Guid.TryParse(upstreamCorrelation, out var parsed) || parsed == Guid.Empty)
                return await Block("Quarantined", "INVALID_UPSTREAM_CORRELATION", upstreamCorrelation);
            upstream = parsed;
        }
        var supplied = new[] { context.CorrelationId, upstream, envelopeCorrelation }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (supplied.Contains(Guid.Empty) || supplied.Distinct().Count() > 1)
            return await Block("Quarantined", "CONFLICTING_CORRELATION", JsonSerializer.Serialize(new { upstreamCorrelation, envelopeCorrelation, context.CorrelationId }));
        var detail = await warehouse.GetAsync(context, outboundId, ct);
        if (!detail.Success) return await Block("Blocked", detail.ErrorCode!, JsonSerializer.Serialize(new { detail.ErrorCode, detail.StatusCode }));
        var body = detail.Value;
        if (body.GetProperty("outboundId").GetString() != outboundId)
            return await Block("Blocked", "SOURCE_ID_MISMATCH", WarehouseShipmentMapper.Canonical(body));
        var previous = await store.FindAsync(context.Scope, WarehouseShipmentMapper.Key(context.Scope, outboundId), ct);
        var observedSnapshot = WarehouseShipmentMapper.Canonical(body);
        var observedHash = WarehouseShipmentMapper.Hash(observedSnapshot);
        if (previous is not null && previous.Hash != observedHash)
            return await Block("Drift", "SOURCE_DRIFT", JsonSerializer.Serialize(new { previousHash = previous.Hash, observedHash, observedSnapshot }));
        if (body.GetProperty("status").GetString() != "ReadyToShip")
            return await Block("Blocked", "SOURCE_NOT_READY", WarehouseShipmentMapper.Canonical(body));
        if (body.GetProperty("lines").GetArrayLength() == 0)
            return await Block("Blocked", "EMPTY_SOURCE_LINES", WarehouseShipmentMapper.Canonical(body));
        var root = previous?.Shipment.CorrelationId ?? (supplied.Length == 0 ? Guid.NewGuid() : supplied[0]);
        var candidate = previous ?? WarehouseShipmentMapper.Map(context.Scope, body, root, DateTimeOffset.UtcNow,
            supplied.Length == 0 ? null : supplied[0]);
        var mapped = candidate.Shipment;
        var validation = await new CreateShipmentValidator().ValidateAsync(new CreateShipmentCommand(new ShipmentModels.Create(
            mapped.SourceModule, mapped.SourceType, mapped.SourceDocumentId, mapped.WarehouseReferenceId, mapped.ShipToReference,
            mapped.PlannedShipAt, mapped.Lines.Select(x => new ShipmentModels.Line(x.LineNumber, x.ItemId, x.SkuId, x.Quantity, x.UomId, x.InventoryReferenceId)).ToArray(),
            mapped.PlannedDeliverAt)), ct);
        if (!validation.IsValid) return await Block("Blocked", "SHIPMENT_MAPPING_INCOMPATIBLE", candidate.Snapshot);
        var intent = await store.PrepareAsync(context.Scope, candidate, ct);
        if (intent.Hash != candidate.Hash)
            return await Block("Drift", "SOURCE_DRIFT", JsonSerializer.Serialize(new { previousHash = intent.Hash, observedHash = candidate.Hash, observedSnapshot = candidate.Snapshot }));
        if (supplied.Length > 0 && intent.Shipment.CorrelationId != supplied[0])
            return await Block("Quarantined", "CONFLICTING_PERSISTED_ROOT", JsonSerializer.Serialize(new { upstreamCorrelation, envelopeCorrelation, supplied = supplied[0], persisted = intent.Shipment.CorrelationId }));
        try
        {
            var result = await shipments.MutateAsync(context.Scope, null, "create", intent.Key, intent.Hash,
                intent.Shipment.CorrelationId, _ => new ShipmentChange(intent.Shipment, null, 201, intent.Shipment.CreatedAt), ct, intent);
            return new SourceIntakeResult(result.ErrorCode is null ? "Committed" : "Blocked", result.Shipment, result.Replay, result.ErrorCode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Pending intent retains defaults and identity; transactional source link and aggregate rolled back.
            return await Block("Blocked", "COMMIT_FAILED", exception.GetType().Name);
        }
        async Task<SourceIntakeResult> Block(string state, string code, string evidence)
        {
            await store.RecordEvidenceAsync(context.Scope, WarehouseShipmentMapper.Key(context.Scope, outboundId), outboundId,
                state, JsonSerializer.Serialize(new { code, evidence }), ct);
            return new SourceIntakeResult(state, null, false, code);
        }
    }
}
