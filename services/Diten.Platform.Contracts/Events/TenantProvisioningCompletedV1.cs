using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantProvisioningCompletedV1 : IInternalEvent
{
    public const string Name = "tenant.provisioning.completed.v1";
    public const int Version = 1;

    public TenantProvisioningCompletedV1(
        Guid tenantId,
        DateTimeOffset completedAtUtc,
        IReadOnlyList<string> steps)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        CompletedAtUtc = TenantLifecycleEventContractGuards.RequireUtc(completedAtUtc, nameof(completedAtUtc));
        Steps = TenantLifecycleEventContractGuards.RequireSteps(steps);
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public IReadOnlyList<string> Steps { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
