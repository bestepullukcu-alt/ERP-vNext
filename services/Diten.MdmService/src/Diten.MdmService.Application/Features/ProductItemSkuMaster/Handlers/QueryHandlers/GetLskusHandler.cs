using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetLskusHandler
    : IRequestHandler<GetLskusQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>>
{
    private readonly ILskuRepository _lskus;
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetLskusHandler(
        ILskuRepository lskus,
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _lskus = lskus;
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>> Handle(
        GetLskusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToUpperInvariant();
        var scope = await _scopeGuard.ResolveContextAsync("mdm.lskus.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }
        var page = scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation
            ? await _lskus.GetPageAsync(request.PageNumber, request.PageSize, search, cancellationToken)
            : await _lskus.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken);
        var gskus = await _gskus.GetByIdsAsync(
            page.Items.Select(x => x.GskuId).Distinct().ToArray(),
            cancellationToken);
        var gskuById = gskus.ToDictionary(x => x.Id);
        if (page.Items.Any(x => !gskuById.ContainsKey(x.GskuId)))
        {
            return Fail("LSKU_PARENT_BINDING_INVARIANT_VIOLATION");
        }

        var items = page.Items.Select(lsku =>
        {
            var gsku = gskuById[lsku.GskuId];
            return new ProductItemSkuMasterModels.LskuListItemDto(
                lsku.Id,
                lsku.CanonicalCode,
                lsku.GskuId,
                gsku.CanonicalCode,
                lsku.MarketCode,
                lsku.LifecycleStatus,
                lsku.Version,
                lsku.CreatedAt,
                lsku.UpdatedAt);
        }).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>> Fail(
        string code) =>
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.LskuListItemDto>>.Fail(code, 409);
}
