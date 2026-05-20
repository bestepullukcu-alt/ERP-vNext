using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantReactivatedV1 : IInternalEvent
{
    public const string Name = "tenant.reactivated.v1";
    public const int Version = 1;

    public TenantReactivatedV1(
        Guid tenantId,
        DateTimeOffset reactivatedAtUtc,
        Guid? reactivatedBy)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        ReactivatedAtUtc = TenantLifecycleEventContractGuards.RequireUtc(reactivatedAtUtc, nameof(reactivatedAtUtc));
        ReactivatedBy = reactivatedBy;
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset ReactivatedAtUtc { get; init; }
    public Guid? ReactivatedBy { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
