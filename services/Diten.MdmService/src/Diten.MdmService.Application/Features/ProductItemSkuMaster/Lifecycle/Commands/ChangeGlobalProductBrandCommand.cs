using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

/// <summary>
/// MOD-0290-FU05 slice 2 (owner Karar a, 2026-10-15) — an approved Global Product's brand, changed directly and audited
/// (<c>GlobalProductBrandChanged</c>) under <c>mdm.global-products.update-brand</c>; no approval flow. The identity (name,
/// code) does not change. CT decision (A, 2026-10-15): born fenced — an approved product's brand is a write inside the
/// tenant's Legal Entity scope, admitted by the scope write fence and its lease like the other product writers.
/// </summary>
public sealed record ChangeGlobalProductBrandCommand(
    Guid GlobalProductId,
    ProductItemSkuMasterModels.ChangeGlobalProductBrandRequest Request,
    Guid OperationId)
    : IRequest<Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>>,
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation;
