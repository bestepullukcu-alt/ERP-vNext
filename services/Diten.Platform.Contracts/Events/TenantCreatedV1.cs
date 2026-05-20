using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantCreatedV1 : IInternalEvent
{
    public const string Name = "tenant.created.v1";
    public const int Version = 1;

    public TenantCreatedV1(
        Guid tenantId,
        DateTimeOffset createdAtUtc,
        Guid? planId,
        Guid? createdBy,
        string tenantDisplayName,
        string locale,
        Guid? initialAdminUserId)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        CreatedAtUtc = TenantLifecycleEventContractGuards.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        PlanId = planId;
        CreatedBy = createdBy;
        TenantDisplayName = TenantLifecycleEventContractGuards.RequireText(tenantDisplayName, nameof(tenantDisplayName), 200);
        Locale = TenantLifecycleEventContractGuards.RequireText(locale, nameof(locale), 35);
        InitialAdminUserId = initialAdminUserId;
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public Guid? PlanId { get; init; }
    public Guid? CreatedBy { get; init; }
    public string TenantDisplayName { get; init; }
    public string Locale { get; init; }
    public Guid? InitialAdminUserId { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
