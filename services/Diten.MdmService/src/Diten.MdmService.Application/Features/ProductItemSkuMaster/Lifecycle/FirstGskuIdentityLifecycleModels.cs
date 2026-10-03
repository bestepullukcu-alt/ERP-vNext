using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class FirstGskuIdentityLifecyclePermissions
{
    public const string Submit = "mdm.gskus.submit";
    public const string Update = "mdm.gskus.update";
    public const string Withdraw = "mdm.gskus.withdraw";
}

public sealed record StartFirstGskuIdentityWorkflowRequest(
    Guid GskuId,
    int ExpectedGskuVersion,
    Guid OperationId);

public sealed record FirstGskuIdentityLifecycleResult(
    Guid ProductDefinitionRevisionId,
    Guid GskuId,
    ProductIdentityLifecycleStatus RevisionLifecycleStatus,
    ProductIdentityLifecycleStatus GskuLifecycleStatus,
    int RevisionVersion,
    int GskuVersion,
    Guid? WorkflowInstanceId,
    bool IsReplay,
    // Shared item 14 (A1): a withdrawal refused because an earlier one stands under another command names that
    // command, so the requester (only the submitter reaches this) can repeat it instead of being stuck.
    Guid? PendingWithdrawalCommandId = null);

public sealed record WithdrawFirstGskuIdentityApprovalRequest(
    Guid GskuId,
    int ExpectedGskuVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);
