using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public sealed record FinishedGoodIdentityWorkflowReservation(
    Guid OperationId,
    Guid FinishedGoodId,
    Guid GskuId,
    Guid ProductDefinitionRevisionId,
    Guid MakerSubjectId,
    int ExpectedFinishedGoodVersion,
    string StartIdempotencyKey,
    string OperationFingerprint,
    FinishedGoodLifecycleAdmissionScopeSnapshot AdmissionScopeSnapshot,
    long? DueAtUtcTicksV1 = null);

public sealed record FinishedGoodIdentityWorkflowReserveResult(
    bool Succeeded,
    bool IsReplay,
    FinishedGoodIdentityWorkflowOperation? Operation,
    string? ErrorCode);

public sealed record FinishedGoodIdentityWorkflowClaim(
    Guid TenantId,
    Guid OperationId,
    Guid FinishedGoodId,
    Guid GskuId,
    Guid ProductDefinitionRevisionId,
    string OperationFingerprint,
    string LeaseOwner,
    long LeaseGeneration,
    FinishedGoodIdentityWorkflowCheckpoint Checkpoint,
    long LeaseUntilUtcTicksV1);

public sealed record FinishedGoodIdentityWorkflowClaimRequest(
    Guid OperationId,
    string OperationFingerprint,
    IReadOnlyCollection<FinishedGoodIdentityWorkflowCheckpoint> EligibleCheckpoints,
    string LeaseOwner,
    long ExpectedLeaseGeneration,
    long LeaseDurationTicks);

public sealed record FinishedGoodIdentityWorkflowCheckpointMutation(
    FinishedGoodIdentityWorkflowCheckpoint NextCheckpoint,
    ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition,
    long? NextAttemptAtUtcTicksV1 = null,
    string? LastFailureCode = null,
    bool ReleaseLease = false);

public sealed record FinishedGoodIdentityWorkflowAdvanceResult(
    bool Succeeded,
    FinishedGoodIdentityWorkflowOperation? Operation,
    string? ErrorCode);

public sealed record FinishedGoodIdentityWorkflowTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextAfterTenantId);

public sealed record FinishedGoodIdentityWorkflowRecoveryCursor(
    long? NextAttemptAtUtcTicksV1,
    Guid OperationId);

public sealed record FinishedGoodIdentityWorkflowRecoverablePage(
    IReadOnlyList<FinishedGoodIdentityWorkflowOperation> Operations,
    FinishedGoodIdentityWorkflowRecoveryCursor? NextCursor);
