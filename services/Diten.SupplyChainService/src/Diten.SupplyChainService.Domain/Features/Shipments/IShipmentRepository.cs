namespace Diten.SupplyChainService.Domain.Features.Shipments;
public interface IShipmentRepository
{
    Task<ShipmentDetailReadResult?> GetDetailAsync(ShipmentScope scope, Guid id, CancellationToken ct);
    Task<Shipment?> GetAsync(ShipmentScope scope, Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Shipment> Items, long Total)> QueryAsync(ShipmentScope scope, ShipmentStatus? status, string? sourceDocumentId, int page, int pageSize, CancellationToken ct);
    Task<ShipmentMutationResult> MutateAsync(ShipmentScope scope, Guid? shipmentId, string operation, string key, string fingerprint,
     Guid correlationId, Func<Shipment?, ShipmentChange> decide, CancellationToken ct, Diten.SupplyChainService.Domain.Features.SourceIntake.SourceIntent? source = null);

    // MOD-0183 §23.4 (DL-1). Every write below is filtered on tenant, legal entity and shipment id; the attempt writes
    // also on the attempt number and lease id the writer holds, so a writer that lost the lease changes nothing.

    /// <summary>The stored answer of an earlier command with this key, or null. Same rules as the replay inside MutateAsync.</summary>
    Task<ShipmentMutationResult?> FindReceiptAsync(ShipmentScope scope, string operation, string key, string fingerprint, Guid correlationId, CancellationToken ct);
    /// <summary>Opens or resumes (takes the lease of) an attempt on a Planned shipment whose current attempt is still <paramref name="observed"/>.</summary>
    Task<bool> TryBeginOrResumeDispatchAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt? observed, DispatchAttempt next, CancellationToken ct);
    /// <summary>Writes the holder's attempt (sent marker, issue, reversal, lease renewal, CompensationFailed) if it still holds N and the lease.</summary>
    Task<bool> SaveDispatchAttemptAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt attempt, Guid heldLeaseId, CancellationToken ct);
    /// <summary>Moves the held attempt, closed as Compensated, to the append-only history; the next attempt gets N + 1.</summary>
    Task<bool> CloseCompensatedAttemptAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt attempt, Guid heldLeaseId, CancellationToken ct);
    /// <summary>Removes a held attempt that posted nothing (an authorization or definite dependency refusal of its first line).</summary>
    Task<bool> DropUnpostedAttemptAsync(ShipmentScope scope, Guid shipmentId, int n, Guid heldLeaseId, CancellationToken ct);
    /// <summary>Records a late writer's movement, without the lease condition (§23.4 late writer).</summary>
    Task AppendLateWriteAsync(ShipmentScope scope, Guid shipmentId, DispatchLateWrite write, CancellationToken ct);
    /// <summary>Stores the outcome of a compensated attempt as the command's receipt, so a replay answers the same.</summary>
    Task StoreOutcomeReceiptAsync(ShipmentScope scope, Guid shipmentId, string operation, string key, string fingerprint, Guid correlationId,
     int statusCode, string errorCode, string? detailsJson, CancellationToken ct);
    /// <summary>Writes an in-product audit row of a dispatch attempt outcome (reversal ids, integrity faults).</summary>
    Task AppendDispatchAuditAsync(ShipmentScope scope, Guid shipmentId, DispatchAuditEntry entry, CancellationToken ct);
}

public sealed record DispatchAuditEntry(int N, string Outcome, Guid CorrelationId, DateTimeOffset OccurredAt,
    IReadOnlyList<string> IssueTransactionIds, IReadOnlyList<string> ReversalTransactionIds, IReadOnlyList<string> IntegrityFaults);
