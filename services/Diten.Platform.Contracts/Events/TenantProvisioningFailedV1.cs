using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantProvisioningFailedV1 : IInternalEvent
{
    public const string Name = "tenant.provisioning.failed.v1";
    public const int Version = 1;

    public TenantProvisioningFailedV1(
        Guid tenantId,
        DateTimeOffset failedAtUtc,
        string failedStep,
        string error,
        int attemptCount)
    {
        TenantId = TenantLifecycleEventContractGuards.RequireTenantId(tenantId);
        FailedAtUtc = TenantLifecycleEventContractGuards.RequireUtc(failedAtUtc, nameof(failedAtUtc));
        FailedStep = TenantLifecycleEventContractGuards.RequireText(failedStep, nameof(failedStep), 128);
        Error = TenantLifecycleEventContractGuards.RedactSensitiveError(error);
        if (attemptCount < 1)
        {
            throw new ArgumentException("AttemptCount must be greater than or equal to 1.", nameof(attemptCount));
        }

        AttemptCount = attemptCount;
    }

    public Guid TenantId { get; init; }
    public DateTimeOffset FailedAtUtc { get; init; }
    public string FailedStep { get; init; }
    public string Error { get; init; }
    public int AttemptCount { get; init; }
    public string EventName => Name;

    public int EventVersion => Version;
}
