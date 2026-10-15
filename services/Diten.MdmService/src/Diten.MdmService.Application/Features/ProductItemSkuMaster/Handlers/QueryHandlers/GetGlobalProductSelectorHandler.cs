using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductSelectorHandler : IRequestHandler<GetGlobalProductSelectorQuery, Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>>
{
    private readonly IGlobalProductRepository _repository;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IBrandRepository? _brands;

    public GetGlobalProductSelectorHandler(
        IGlobalProductRepository repository,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        IBrandRepository? brands = null)
    {
        _repository = repository;
        _brands = brands;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates,
            policies,
            candidates,
            tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>> Handle(
        GetGlobalProductSelectorQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : GlobalProductNameRules.NormalizeDuplicateKey(request.Search);
        var scope = await _scopeGuard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }

        var page = scope.Context!.RolloutMode switch
        {
            ProductLegalEntityScopeRolloutMode.Preparation => await _repository.GetPageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                lifecycleStatus: null,
                cancellationToken),
            ProductLegalEntityScopeRolloutMode.Enforced => await _repository.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                lifecycleStatus: null,
                referenceableOnly: false,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken),
            _ => new GlobalProductPage([], 0)
        };
        // MOD-0290-FU05 (Karar b): one brand read per page.
        var brands = await GlobalProductBrands.ReadAsync(_brands, page.Items.Select(x => x.BrandId), cancellationToken);
        var items = page.Items.Select(x =>
        {
            var brand = GlobalProductBrands.Describe(x.BrandId, brands);
            return new ProductItemSkuMasterModels.GlobalProductSelectorDto(
                x.Id,
                x.CanonicalCode,
                x.GlobalProductName,
                brand.Id,
                brand.Code,
                brand.Name);
        }).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }
}
