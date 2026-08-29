using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes.Handlers;

public sealed class GetTerritoryHierarchyHandler : IRequestHandler<GetTerritoryHierarchyQuery, Response<TerritoryHierarchyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;

    public GetTerritoryHierarchyHandler(ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
    }

    public async Task<Response<TerritoryHierarchyDto>> Handle(GetTerritoryHierarchyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryHierarchyDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryHierarchyDto>.Fail("Territory model not found.", 404);
        }

        var nodes = await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var dto = new TerritoryHierarchyDto(request.ModelId, nodes.Select(TerritoryNodeMapper.ToDto).ToList());
        return Response<TerritoryHierarchyDto>.Success(dto);
    }
}
