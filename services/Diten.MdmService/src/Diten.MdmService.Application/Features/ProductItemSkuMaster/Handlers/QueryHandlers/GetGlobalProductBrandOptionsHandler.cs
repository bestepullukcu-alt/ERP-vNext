using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

/// <summary>
/// MOD-0290-FU05 (T-1) — only the brands a Global Product write would accept: the same <c>Brand.IsLinkableAt</c> the
/// link rule (<c>ProductBrandLink.Refusal</c>) uses, so the picker never offers a brand the save then refuses. The brand
/// register's own read (<c>GetBrandListHandler</c>) loads the tenant's brands and pages them in memory; this does the same.
/// </summary>
public sealed class GetGlobalProductBrandOptionsHandler
    : IRequestHandler<GetGlobalProductBrandOptionsQuery, Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductBrandOptionDto>>>
{
    private readonly IBrandRepository _brands;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public GetGlobalProductBrandOptionsHandler(IBrandRepository brands, ITenantContext tenantContext, TimeProvider? clock = null)
    {
        _brands = brands;
        _tenantContext = tenantContext;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductBrandOptionDto>>> Handle(
        GetGlobalProductBrandOptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductBrandOptionDto>>
                .Fail("TENANT_CONTEXT_REQUIRED", 400);
        }

        var now = _clock.GetUtcNow();
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var linkable = (await _brands.GetAllAsync(cancellationToken))
            .Where(brand => brand.IsLinkableAt(now))
            .Where(brand => search is null
                            || brand.BrandCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || brand.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(brand => brand.BrandCode, StringComparer.Ordinal)
            .ThenBy(brand => brand.Id)
            .ToList();
        var items = linkable
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(brand => new ProductItemSkuMasterModels.GlobalProductBrandOptionDto(brand.Id, brand.BrandCode, brand.BrandName))
            .ToList();

        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductBrandOptionDto>>.Success(
            new(items, request.PageNumber, request.PageSize, linkable.Count));
    }
}
