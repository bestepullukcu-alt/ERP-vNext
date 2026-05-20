using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantCancelledV1 : IInternalEvent
{
    public const string Name = "tenant.cancelled.v1";
    public const int Version = 1;

    public TenantCancelledV1(
        Guid tenantId,
        DateTimeOffset cancelledAtUtc,
        DateTimeOffset effectiveAtUtc,
        string? reason,
        Guid? cancelledBy)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        CancelledAtUtc = TenantLifecycleEventContractGuards.RequireUtc(cancelledAtUtc, nameof(cancelledAtUtc));
        EffectiveAtUtc = TenantLifecycleEventContractGuards.RequireUtc(effectiveAtUtc, nameof(effectiveAtUtc));
        if (EffectiveAtUtc < CancelledAtUtc)
        {
            throw new ArgumentException("EffectiveAtUtc must be greater than or equal to CancelledAtUtc.", nameof(effectiveAtUtc));
        }

        Reason = TenantLifecycleEventContractGuards.OptionalText(reason, 500);
        CancelledBy = cancelledBy;
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset CancelledAtUtc { get; init; }
    public DateTimeOffset EffectiveAtUtc { get; init; }
    public string? Reason { get; init; }
    public Guid? CancelledBy { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
