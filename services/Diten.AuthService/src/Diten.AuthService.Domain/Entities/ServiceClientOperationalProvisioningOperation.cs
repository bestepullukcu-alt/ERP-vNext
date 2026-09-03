namespace Diten.AuthService.Domain.Entities;

public enum ServiceClientOperationalProvisioningState
{
    Pending = 1,
    RecoveryRequired = 2,
    Completed = 3
}

public enum ServiceClientOperationalProvisioningCheckpoint
{
    Reserved = 1,
    TargetMutated = 2,
    ReadBackVerified = 3,
    EvidenceRecorded = 4
}

public sealed class ServiceClientOperationalProvisioningEvidence
{
    public int Sequence { get; init; }
    public string EventType { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public long TargetOperationalVersion { get; init; }
}

public sealed class ServiceClientOperationalProvisioningOperation : GlobalEntityBase
{
    public Guid CommandId { get; init; }
    public string CommandFingerprint { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public Guid TargetId { get; init; }
    public Guid? TenantId { get; init; }
    public string ClientCode { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public long ExpectedOperationalVersion { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public ServiceClientOperationalProvisioningState State { get; set; }
    public ServiceClientOperationalProvisioningCheckpoint Checkpoint { get; set; }
    public int EvidenceSequence { get; set; }
    public IReadOnlyList<ServiceClientOperationalProvisioningEvidence> Evidence { get; set; } = [];
}
