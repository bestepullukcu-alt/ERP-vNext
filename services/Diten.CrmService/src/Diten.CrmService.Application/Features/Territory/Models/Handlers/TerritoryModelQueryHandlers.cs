using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models.Handlers;

public sealed class GetTerritoryModelListHandler : IRequestHandler<GetTerritoryModelListQuery, Response<TerritoryModelListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;

    public GetTerritoryModelListHandler(ITenantContext tenant, ITerritoryModelRepository models)
    {
        _tenant = tenant;
        _models = models;
    }

    public async Task<Response<TerritoryModelListDto>> Handle(GetTerritoryModelListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryModelListDto>.Fail("Tenant context is required.", 400);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        var (items, total) = await _models.ListAsync(tenantId, request.Search, request.Status, page, pageSize, cancellationToken);
        var dto = new TerritoryModelListDto(items.Select(TerritoryModelMapper.ToListItem).ToList(), total, page, pageSize);
        return Response<TerritoryModelListDto>.Success(dto);
    }
}

public sealed class GetTerritoryModelByIdHandler : IRequestHandler<GetTerritoryModelByIdQuery, Response<TerritoryModelDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;

    public GetTerritoryModelByIdHandler(ITenantContext tenant, ITerritoryModelRepository models)
    {
        _tenant = tenant;
        _models = models;
    }

    public async Task<Response<TerritoryModelDetailDto>> Handle(GetTerritoryModelByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryModelDetailDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryModelDetailDto>.Fail("Territory model not found.", 404);
        }

        return Response<TerritoryModelDetailDto>.Success(TerritoryModelMapper.ToDetail(model));
    }
}
