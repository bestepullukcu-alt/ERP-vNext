using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class FirstGskuIdentityRetirementOperation : EntityBase
{
    public const int CurrentTemporalStorageVersion = 1;

    public Guid OperationId { get; set; }
    public string OperationFingerprint { get; set; } = string.Empty;
    public Guid ProductDefinitionRevisionId { get; set; }
    public Guid GskuId { get; set; }
    public int ExpectedRevisionVersion { get; set; }
    public int ExpectedGskuVersion { get; set; }
    public Guid ActorSubjectId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public FirstGskuIdentityRetirementCheckpoint Checkpoint { get; set; }
    public ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition { get; set; }
    public long? NextAttemptAtUtcTicksV1 { get; set; }
    public string? LastFailureCode { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public long LeaseGeneration { get; set; }
    public int TemporalStorageVersion { get; set; } = CurrentTemporalStorageVersion;
    public long CreatedAtUtcTicksV1 { get; set; }
    public long UpdatedAtUtcTicksV1 { get; set; }
}
