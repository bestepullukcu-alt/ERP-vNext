using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class FinishedGoodIdentityWorkflowOperation : EntityBase
{
    public const int CurrentTemporalStorageVersion = 1;

    public Guid OperationId { get; init; }
    public Guid FinishedGoodId { get; init; }
    public Guid GskuId { get; init; }
    public Guid ProductDefinitionRevisionId { get; init; }
    public Guid MakerSubjectId { get; init; }
    public int ExpectedFinishedGoodVersion { get; init; }
    public string StartIdempotencyKey { get; init; } = string.Empty;
    public string OperationFingerprint { get; init; } = string.Empty;
    public FinishedGoodLifecycleAdmissionScopeSnapshot AdmissionScopeSnapshot { get; init; } = null!;

    public FinishedGoodIdentityWorkflowCheckpoint Checkpoint { get; set; } =
        FinishedGoodIdentityWorkflowCheckpoint.Prepared;
    public ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition { get; set; } =
        ProductIdentityWorkflowRecoveryDisposition.None;
    public long? DueAtUtcTicksV1 { get; set; }
    public long? NextAttemptAtUtcTicksV1 { get; set; }
    public string? LastFailureCode { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public long LeaseGeneration { get; set; }
    public int TemporalStorageVersion { get; init; } = CurrentTemporalStorageVersion;
    public long CreatedAtUtcTicksV1 { get; set; }
    public long UpdatedAtUtcTicksV1 { get; set; }
}
