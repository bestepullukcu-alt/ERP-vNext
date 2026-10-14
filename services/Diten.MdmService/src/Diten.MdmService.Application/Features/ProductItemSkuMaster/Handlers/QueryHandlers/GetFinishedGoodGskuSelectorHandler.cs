using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetFinishedGoodGskuSelectorHandler
    : IRequestHandler<GetFinishedGoodGskuSelectorQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetFinishedGoodGskuSelectorHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>> Handle(
        GetFinishedGoodGskuSelectorQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToUpperInvariant();
        var scope = await _scopeGuard.ResolveContextAsync("mdm.finished-goods.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }
        var page = scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation
            ? await _gskus.GetReferenceablePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                cancellationToken)
            : await _gskus.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                referenceableOnly: true,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken);
        var items = page.Items.Select(gsku => new ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto(
            gsku.Id,
            gsku.CanonicalCode,
            gsku.CanonicalCode)).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.FinishedGoodGskuSelectorDto>>
            .Success(new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }
}
