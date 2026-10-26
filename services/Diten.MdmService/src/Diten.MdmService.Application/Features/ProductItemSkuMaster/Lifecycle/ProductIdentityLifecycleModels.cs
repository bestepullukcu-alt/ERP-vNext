using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class ProductIdentityLifecyclePermissions
{
    public const string GlobalProductSubmit = "mdm.global-products.submit";
    public const string GlobalProductRetire = "mdm.global-products.retire";
    public const string GlobalProductWithdraw = "mdm.global-products.withdraw";
    public const string GlobalProductRequestCorrection = "mdm.global-products.request-correction";
    public const string GlobalProductRequestRetirement = "mdm.global-products.request-retirement";
    // MOD-0290-FU05 slice 2 (owner Karar a): an approved product's brand, by its own key (PKS-001 Tier-3).
    public const string GlobalProductUpdateBrand = "mdm.global-products.update-brand";
}

public sealed record RetireGlobalProductIdentityRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    Guid OperationId,
    string ReasonCode,
    string? Comment);

public sealed record WithdrawGlobalProductIdentityApprovalRequest(
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
