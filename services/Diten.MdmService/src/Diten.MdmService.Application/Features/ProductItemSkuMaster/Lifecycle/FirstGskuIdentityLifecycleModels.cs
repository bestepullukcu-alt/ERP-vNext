using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class FirstGskuIdentityLifecyclePermissions
{
    public const string Submit = "mdm.gskus.submit";
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
