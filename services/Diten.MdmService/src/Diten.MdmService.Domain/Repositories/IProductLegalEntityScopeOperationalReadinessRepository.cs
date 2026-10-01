using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopeOperationalReadinessRepository
{
    Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(
        int maximumSampleSize,
        CancellationToken cancellationToken = default);

    Task<string> CaptureMutationStateHashAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromException<string>(new InvalidOperationException("PRODUCT_SCOPE_MUTATION_SNAPSHOT_NOT_SUPPORTED"));

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
