using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class ProductIdentityLifecyclePermissions
{
    public const string LskuSubmit = "mdm.global-lskus.submit";
    public const string LskuRetire = "mdm.global-lskus.retire";
}

public sealed record SubmitLskuIdentityRequest(
    Guid LskuId,
    int ExpectedVersion,
    ProductIdentityWorkflowBinding WorkflowBinding);

public sealed record ReconcileLskuIdentityDecisionRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid ExpectedWorkflowInstanceId,
    ProductIdentityWorkflowDecisionEvidence DecisionEvidence);

public sealed record RetireLskuIdentityRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record LskuIdentityLifecycleResult(
    Guid LskuId,
    ProductIdentityLifecycleStatus LifecycleStatus,
    int Version,
    Guid? WorkflowInstanceId,
    bool IsReplay);
