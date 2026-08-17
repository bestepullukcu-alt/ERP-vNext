using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantEntitlementEnabledV1 : IInternalEvent
{
    public const string Name = "tenant.entitlement.enabled.v1";
    public const int Version = 1;

    public TenantEntitlementEnabledV1(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid tenantId,
        Guid correlationId,
        Guid? actorId,
        string moduleCode)
    {
        EventId = EntitlementCacheInvalidationEventContractGuards.RequireId(eventId, nameof(eventId));
        OccurredAtUtc = EntitlementCacheInvalidationEventContractGuards.RequireOccurredAtUtc(occurredAtUtc, nameof(occurredAtUtc));
        TenantId = EntitlementCacheInvalidationEventContractGuards.RequireId(tenantId, nameof(tenantId));
        CorrelationId = EntitlementCacheInvalidationEventContractGuards.RequireId(correlationId, nameof(correlationId));
        ActorId = actorId;
        ModuleCode = EntitlementCacheInvalidationEventContractGuards.RequireModuleCode(moduleCode);
    }

    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? ActorId { get; init; }
    public string ModuleCode { get; init; }
    public string EventName => Name;
    public int EventVersion => Version;
}
