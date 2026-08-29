using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class FinishedGoodIdentityLifecyclePermissions
{
    public const string Submit = "mdm.finished-goods.submit";
    public const string Retire = "mdm.finished-goods.retire";
}

public sealed record StartFinishedGoodIdentityWorkflowRequest(
    Guid FinishedGoodId,
    int ExpectedVersion,
    Guid OperationId);

public sealed record RetireFinishedGoodIdentityRequest(
    Guid FinishedGoodId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record FinishedGoodIdentityWorkflowResult(
    Guid OperationId,
    Guid FinishedGoodId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    string RecoveryDisposition,
    bool IsReplay);

public sealed record FinishedGoodIdentityLifecycleResult(
    Guid FinishedGoodId,
    ProductIdentityLifecycleStatus LifecycleStatus,
    int Version,
    Guid? WorkflowInstanceId,
    bool IsReplay);
