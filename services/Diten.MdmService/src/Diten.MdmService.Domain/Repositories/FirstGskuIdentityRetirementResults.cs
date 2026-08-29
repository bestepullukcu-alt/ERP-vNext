using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record FirstGskuIdentityRetirementReserveResult(
    bool Succeeded, bool IsReplay, FirstGskuIdentityRetirementOperation? Operation, string? ErrorCode);

public sealed record FirstGskuIdentityRetirementClaimRequest(
    Guid OperationId, string OperationFingerprint,
    IReadOnlyCollection<FirstGskuIdentityRetirementCheckpoint> EligibleCheckpoints,
    string LeaseOwner, long NowUtcTicks, long LeaseUntilUtcTicks);

public sealed record FirstGskuIdentityRetirementClaim(
    Guid TenantId, Guid OperationId, string OperationFingerprint, string LeaseOwner,
    long LeaseGeneration, FirstGskuIdentityRetirementCheckpoint Checkpoint, long LeaseUntilUtcTicksV1);

public sealed record FirstGskuIdentityRetirementMutation(
    FirstGskuIdentityRetirementCheckpoint NextCheckpoint,
    ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition,
    long UpdatedAtUtcTicks,
    long? NextAttemptAtUtcTicksV1 = null,
    string? LastFailureCode = null,
    bool ReleaseLease = false);

public sealed record FirstGskuIdentityRetirementRecoveryCursor(long? NextAttemptAtUtcTicksV1, Guid OperationId);
public sealed record FirstGskuIdentityRetirementRecoverablePage(
    IReadOnlyList<FirstGskuIdentityRetirementOperation> Operations,
    FirstGskuIdentityRetirementRecoveryCursor? NextCursor);

public sealed record GskuChildCreationAdmissionResult(
    bool Succeeded, bool IsReplay, Gsku? Gsku, string? ErrorCode);

public sealed record FirstGskuIdentityRetirementWriteResult<T>(
    bool Succeeded, bool IsReplay, T? Aggregate, string? ErrorCode) where T : EntityBase;
