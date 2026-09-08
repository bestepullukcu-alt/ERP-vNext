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
    bool IsReplay);

public sealed record WithdrawFirstGskuIdentityApprovalRequest(
    Guid GskuId,
    int ExpectedGskuVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);
