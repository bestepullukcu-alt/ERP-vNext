using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantSuspendedV1 : IInternalEvent
{
    public const string Name = "tenant.suspended.v1";
    public const int Version = 1;

    public TenantSuspendedV1(
        Guid tenantId,
        DateTimeOffset suspendedAtUtc,
        string reason,
        Guid? suspendedBy)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        SuspendedAtUtc = TenantLifecycleEventContractGuards.RequireUtc(suspendedAtUtc, nameof(suspendedAtUtc));
        Reason = TenantLifecycleEventContractGuards.RequireText(reason, nameof(reason), 500);
        SuspendedBy = suspendedBy;
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset SuspendedAtUtc { get; init; }
    public string Reason { get; init; }
    public Guid? SuspendedBy { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
