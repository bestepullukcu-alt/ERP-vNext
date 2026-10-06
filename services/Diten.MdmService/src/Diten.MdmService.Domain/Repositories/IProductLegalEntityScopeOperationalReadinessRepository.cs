using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopeOperationalReadinessRepository
{
    Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(
        int maximumSampleSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B4 — the state of what ONE write touches: the tenant's documents of the product
    /// collections whose id or parent id is one of <paramref name="objectIds"/> (indexed lookups, no collection read in
    /// full). <c>Verifiable</c> is false when the write names no object (then nothing proves it changed nothing).
    /// </summary>
    Task<ProductLegalEntityScopeObjectState> CaptureObjectStateHashAsync(
        IReadOnlyCollection<Guid> objectIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductLegalEntityScopeObjectState(string.Empty, false));


    Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(
        ProductLegalEntityScopeInventorySnapshotRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<ProductLegalEntityScopeInventorySnapshot>(
            new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_NOT_SUPPORTED"));
}

public sealed record ProductLegalEntityScopeInventorySnapshotRequest(
    Guid TenantId,
    Guid RolloutStateId,
    ProductLegalEntityScopeRolloutMode RolloutMode,
    int RolloutVersion,
    string Action,
    Guid CommandId,
    Guid ActorId,
    string ReasonCode,
    DateTimeOffset ObservedAtUtc);

public sealed record ProductLegalEntityScopeOperationalReadiness(
    ProductLegalEntityScopeOperationalRolloutFact? Rollout,
    IReadOnlyList<ProductLegalEntityScopeIntegrityCategory> DescendantIntegrity,
    IReadOnlyList<ProductLegalEntityScopeAuditCategory> Audit);

public sealed record ProductLegalEntityScopeOperationalRolloutFact(
    Guid Id,
    ProductLegalEntityScopeRolloutMode Mode,
    int Version,
    Guid CreationCommandId,
    Guid CreatedByActorId);

public sealed record ProductLegalEntityScopeIntegrityCategory(
    string Category,
    long TotalCount,
    long OrphanCount,
    IReadOnlyList<Guid> OrphanIds,
    bool HasMore);

public sealed record ProductLegalEntityScopeAuditCategory(
    AuditAggregateType AggregateType,
    long PendingCount,
    long ProcessingCount,
    long DeliveredCount,
    long DeadLetterCount,
    long CompactedReceiptCount,
    long MalformedCount,
    long UnacknowledgedCount,
    IReadOnlyList<Guid> MalformedIntentIds,
    bool HasMoreMalformed,
    IReadOnlyList<Guid> UnacknowledgedIntentIds,
    bool HasMoreUnacknowledged);

public sealed record ProductLegalEntityScopeObjectState(string Hash, bool Verifiable);
