using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

// LSKU FIX1 B3 (PACK DRAFT_CHILD_CANCELLATION_REQUIRED): a draft LSKU is cancelled by the key that creates it, so a
// draft no longer blocks its parent GSKU's retirement for good.
public sealed record CancelLskuDraftCommand(Guid LskuId, int ExpectedVersion, Guid OperationId, string ReasonCode)
    : IRequest<Response<LskuDraftCancellationResult>>, IAuditableCommand, IAuditMetadataProvider,
      // WP-MDM-SCOPE-INT-01 (CT decision): born fenced — no product write while the scope rollout measures the inventory.
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Delete,
        EntityType: "Lsku", SourceModule: "product-item-sku-master");
}

public sealed record LskuDraftCancellationResult(Guid LskuId, int Version, bool AdmissionReleased, bool IsReplay);
