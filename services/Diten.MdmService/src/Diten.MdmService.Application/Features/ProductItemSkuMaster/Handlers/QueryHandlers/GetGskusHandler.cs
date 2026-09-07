using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGskusHandler
    : IRequestHandler<GetGskusQuery,
        Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetGskusHandler(
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

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>> Handle(
        GetGskusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim().ToUpperInvariant();
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.read", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }

        var page = scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation
            ? await _gskus.GetPageAsync(request.PageNumber, request.PageSize, search, cancellationToken)
            : await _gskus.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                referenceableOnly: false,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken);
        var revisions = await _revisions.GetByIdsAsync(
            page.Items.Select(x => x.ProductDefinitionRevisionId).Distinct().ToArray(),
            cancellationToken);
        var revisionById = revisions.ToDictionary(x => x.Id);
        if (page.Items.Any(x => !revisionById.ContainsKey(x.ProductDefinitionRevisionId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION");
        }

        var products = await _globalProducts.GetByIdsAsync(
            revisions.Select(x => x.GlobalProductId).Distinct().ToArray(),
            cancellationToken);
        var productById = products.ToDictionary(x => x.Id);
        if (revisions.Any(x => !productById.ContainsKey(x.GlobalProductId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION");
        }

        var items = page.Items.Select(gsku =>
        {
            var revision = revisionById[gsku.ProductDefinitionRevisionId];
            var product = productById[revision.GlobalProductId];
            return new ProductItemSkuMasterModels.GskuListItemDto(
                gsku.Id,
                gsku.CanonicalCode,
                product.Id,
                product.CanonicalCode,
                product.GlobalProductName,
                revision.Id,
                revision.RevisionIdentifier,
                gsku.PackQuantity,
                gsku.PackUomCode,
                gsku.LifecycleStatus,
                gsku.Version,
                gsku.CreatedAt,
                gsku.UpdatedAt);
        }).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>> Fail(
        string code) => Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>
        .Fail(code, 409);
}
