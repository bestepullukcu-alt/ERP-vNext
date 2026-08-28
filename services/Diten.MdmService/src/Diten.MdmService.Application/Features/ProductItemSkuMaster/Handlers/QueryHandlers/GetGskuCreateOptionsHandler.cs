using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGskuCreateOptionsHandler
    : IRequestHandler<GetGskuCreateOptionsQuery, Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>>
{
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IVerifiedGskuReferenceResolver _resolver;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetGskuCreateOptionsHandler(
        IGlobalProductRepository globalProducts,
        IVerifiedGskuReferenceResolver resolver,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _globalProducts = globalProducts;
        _resolver = resolver;
        _scopeGuard = new(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>> Handle(
        GetGskuCreateOptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await _scopeGuard.ResolveContextAsync("mdm.gskus.create", cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Fail(
                scope.FailureCode!,
                scope.StatusCode);
        }
        if (scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation)
        {
            return await GskuCreateOptionsFacade.GetAsync(
                _globalProducts,
                _resolver,
                request.PageNumber,
                request.PageSize,
                request.Search,
                cancellationToken);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : GlobalProductNameRules.NormalizeDuplicateKey(request.Search);
        var page = scope.Context.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
            ? await _globalProducts.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                normalizedSearch,
                lifecycleStatus: null,
                referenceableOnly: true,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken)
            : new GlobalProductPage([], 0);
        var enumeration = await _resolver.EnumerateUomsAsync(cancellationToken);
        if (!enumeration.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Fail(
                enumeration.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                GskuCreateOptionsFacade.NormalizeProviderStatus(enumeration.StatusCode));
        }

        return Response<ProductItemSkuMasterModels.GskuCreateOptionsDto>.Success(new(
            page.Items.Select(product => new ProductItemSkuMasterModels.GskuCreateGlobalProductOptionDto(
                product.Id,
                product.CanonicalCode,
                product.GlobalProductName)).ToList(),
            enumeration.Uoms
                .OrderBy(item => item.SortOrder)
                .Select(item => new ProductItemSkuMasterModels.GskuCreateUomOptionDto(
                    item.Code,
                    item.DisplayText,
                    item.SortOrder,
                    item.MaximumDecimalPrecision)).ToList()));
    }
}
