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

    public GetGlobalProductSelectorHandler(
        IGlobalProductRepository repository,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _repository = repository;
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
        var items = page.Items.Select(x => new ProductItemSkuMasterModels.GlobalProductSelectorDto(
            x.Id,
            x.CanonicalCode,
            x.GlobalProductName)).ToList();
        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductSelectorDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }
}
