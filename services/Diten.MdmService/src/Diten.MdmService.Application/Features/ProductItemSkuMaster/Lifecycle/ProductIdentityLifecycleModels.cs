using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class ProductIdentityLifecyclePermissions
{
    public const string GlobalProductSubmit = "mdm.global-products.submit";
    public const string GlobalProductRetire = "mdm.global-products.retire";
}

public sealed record SubmitGlobalProductIdentityRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    ProductIdentityWorkflowBinding WorkflowBinding);

public sealed record ReconcileGlobalProductIdentityDecisionRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    Guid ExpectedWorkflowInstanceId,
    ProductIdentityWorkflowDecisionEvidence DecisionEvidence);

public sealed record RetireGlobalProductIdentityRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record GlobalProductIdentityLifecycleResult(
    Guid GlobalProductId,
    ProductIdentityLifecycleStatus LifecycleStatus,
    int Version,
    Guid? WorkflowInstanceId,
    bool IsReplay);
