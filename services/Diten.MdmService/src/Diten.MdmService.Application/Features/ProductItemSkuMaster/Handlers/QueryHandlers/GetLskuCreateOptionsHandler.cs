using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetLskuCreateOptionsHandler
    : IRequestHandler<GetLskuCreateOptionsQuery, Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>>
{
    private readonly IGskuRepository _gskus;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedMarketReferenceResolver _markets;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetLskuCreateOptionsHandler(
        IGskuRepository gskus,
        IProductDefinitionRevisionRepository revisions,
        IGlobalProductRepository globalProducts,
        IVerifiedMarketReferenceResolver markets,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _gskus = gskus;
        _revisions = revisions;
        _globalProducts = globalProducts;
        _markets = markets;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>> Handle(
        GetLskuCreateOptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.lskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }
        if (scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation)
        {
            return await LskuCreateOptionsFacade.GetAsync(
                _gskus,
                _revisions,
                _globalProducts,
                _markets,
                request.PageNumber,
                request.PageSize,
                request.Search,
                cancellationToken);
        }

        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToUpperInvariant();
        var page = await _gskus.GetEnforcedLegalEntityScopePageAsync(
            request.PageNumber,
            request.PageSize,
            search,
            referenceableOnly: true,
            scope.Context.EffectiveCandidateLegalEntityIds,
            scope.Context.ServerNowUtc,
            cancellationToken);
        var revisions = await _revisions.GetByIdsAsync(
            page.Items.Select(item => item.ProductDefinitionRevisionId).Distinct().ToArray(),
            cancellationToken);
        var revisionById = revisions.ToDictionary(item => item.Id);
        if (page.Items.Any(item => !revisionById.ContainsKey(item.ProductDefinitionRevisionId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION", 409);
        }
        var products = await _globalProducts.GetByIdsAsync(
            revisions.Select(item => item.GlobalProductId).Distinct().ToArray(),
            cancellationToken);
        var productById = products.ToDictionary(item => item.Id);
        if (revisions.Any(item => !productById.ContainsKey(item.GlobalProductId)))
        {
            return Fail("GSKU_PARENT_BINDING_INVARIANT_VIOLATION", 409);
        }

        var markets = await _markets.EnumerateActiveAsync(cancellationToken);
        if (!markets.IsSuccessful)
        {
            return Fail(
                markets.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                LskuCreateOptionsFacade.NormalizeProviderStatus(markets.StatusCode));
        }

        return Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>.Success(new(
            page.Items.Select(gsku =>
            {
                var revision = revisionById[gsku.ProductDefinitionRevisionId];
                var product = productById[revision.GlobalProductId];
                return new ProductItemSkuMasterModels.LskuCreateGskuOptionDto(
                    gsku.Id,
                    gsku.CanonicalCode,
                    product.CanonicalCode,
                    product.GlobalProductName,
                    revision.RevisionIdentifier,
                    gsku.PackQuantity,
                    gsku.PackUomCode);
            }).ToList(),
            markets.Markets
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .Select(item => new ProductItemSkuMasterModels.LskuCreateMarketOptionDto(
                    item.Code,
                    item.DisplayText,
                    item.SortOrder)).ToList()));
    }

    private static Response<ProductItemSkuMasterModels.LskuCreateOptionsDto> Fail(
        string code,
        int statusCode) => Response<ProductItemSkuMasterModels.LskuCreateOptionsDto>.Fail(code, statusCode);
}
