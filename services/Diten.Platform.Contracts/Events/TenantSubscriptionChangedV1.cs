using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantSubscriptionChangedV1 : IInternalEvent
{
    public const string Name = "tenant.subscription.changed.v1";
    public const int Version = 1;

    public TenantSubscriptionChangedV1(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid tenantId,
        Guid correlationId,
        Guid? actorId,
        Guid? previousPlanId,
        Guid? newPlanId,
        string? previousStatus,
        string? newStatus)
    {
        EventId = EntitlementCacheInvalidationEventContractGuards.RequireId(eventId, nameof(eventId));
        OccurredAtUtc = EntitlementCacheInvalidationEventContractGuards.RequireOccurredAtUtc(occurredAtUtc, nameof(occurredAtUtc));
        TenantId = EntitlementCacheInvalidationEventContractGuards.RequireId(tenantId, nameof(tenantId));
        CorrelationId = EntitlementCacheInvalidationEventContractGuards.RequireId(correlationId, nameof(correlationId));
        ActorId = actorId;
        PreviousPlanId = previousPlanId;
        NewPlanId = newPlanId;
        PreviousStatus = EntitlementCacheInvalidationEventContractGuards.OptionalStatus(previousStatus);
        NewStatus = EntitlementCacheInvalidationEventContractGuards.OptionalStatus(newStatus);
    }

    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? ActorId { get; init; }
    public Guid? PreviousPlanId { get; init; }
    public Guid? NewPlanId { get; init; }
    public string? PreviousStatus { get; init; }
    public string? NewStatus { get; init; }
    public string EventName => Name;
    public int EventVersion => Version;
}
