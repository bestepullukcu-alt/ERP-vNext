using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class LskuIdentityLifecyclePermissions
{
    public const string Submit = "mdm.lskus.submit";
    public const string Withdraw = "mdm.lskus.withdraw";
    public const string Retire = "mdm.lskus.retire";
    // LSKU FIX1 B3: cancelling a draft undoes its creation, so it is held by the key that creates it.
    public const string CancelDraft = "mdm.lskus.create";
    // LSKU FIX1 B1 (c): non-human only (Auth NonHumanPermissionKeys); the manifest's "System" recover action.
    public const string RecoverLifecycleOperation = "mdm.product-identity.lifecycle-operations.recover";
}

public sealed record StartLskuIdentityWorkflowRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId);

public sealed record RetireLskuIdentityRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record WithdrawLskuIdentityApprovalRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record LskuIdentityWorkflowResult(
    Guid OperationId,
    Guid LskuId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    string RecoveryDisposition,
    bool IsReplay);

public sealed record LskuIdentityLifecycleResult(
    Guid LskuId,
    ProductIdentityLifecycleStatus LifecycleStatus,
    int Version,
    Guid? WorkflowInstanceId,
    bool IsReplay);
