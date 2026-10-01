using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GskuPairRetirementPermissions
{
    public const string Retire = "mdm.gskus.retire";
}

public sealed record RetireGskuIdentityPairRequest(
    Guid GskuId,
    int ExpectedGskuVersion,
    Guid OperationId,
    string ReasonCode);

public sealed record GskuPairRetirementResult(
    Guid ProductDefinitionRevisionId,
    Guid GskuId,
    ProductIdentityLifecycleStatus RevisionLifecycleStatus,
    ProductIdentityLifecycleStatus GskuLifecycleStatus,
    int RevisionVersion,
    int GskuVersion,
    bool IsReplay);

public sealed record GskuPairRetirementProcessingResult(
    bool Succeeded,
    Domain.Entities.FirstGskuIdentityRetirementOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);
