using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes.Handlers;

public sealed class UpdateTerritoryNodeHandler : IRequestHandler<UpdateTerritoryNodeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryReferenceValidator _references;

    public UpdateTerritoryNodeHandler(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryReferenceValidator references)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _references = references;
    }

    public async Task<Response<bool>> Handle(UpdateTerritoryNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<bool>.Fail("Territory model not found.", 404);
        }

        if (!string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Nodes can only be edited on a draft territory model.", 409);
        }

        var node = await _nodes.GetByIdAsync(tenantId, request.ModelId, request.Id, cancellationToken);
        if (node is null)
        {
            return Response<bool>.Fail("Territory node not found.", 404);
        }

        TerritoryNode? parent = null;
        if (request.ParentTerritoryId is { } parentId)
        {
            if (parentId == request.Id)
            {
                return Response<bool>.Fail("A node cannot be its own parent.", 400);
            }

            parent = await _nodes.GetByIdAsync(tenantId, request.ModelId, parentId, cancellationToken);
            if (parent is null)
            {
                return Response<bool>.Fail("Parent node not found in this model.", 404);
            }

            if (await _nodes.WouldCreateCycleAsync(tenantId, request.ModelId, request.Id, parentId, cancellationToken))
            {
                return Response<bool>.Fail("Re-parenting to this node would create a circular hierarchy.", 400);
            }
        }

        var levelCode = request.TerritoryLevel.Trim();
        var validationError = await TerritoryNodeValidation.ValidateAsync(
            _references, model, parent, levelCode, node.Status,
            request.EffectiveFrom, request.EffectiveTo, request.MicroZoneProfile is not null, cancellationToken);
        if (validationError is not null)
        {
            return Response<bool>.Fail(validationError.Message, validationError.StatusCode);
        }

        var territoryCode = request.TerritoryCode.Trim();
        if (await _nodes.ExistsByCodeAsync(tenantId, request.ModelId, territoryCode, excludeId: request.Id, cancellationToken))
        {
            return Response<bool>.Fail("TerritoryCode already exists in this model.", 409);
        }

        node.ParentTerritoryId = request.ParentTerritoryId;
        node.TerritoryCode = territoryCode;
        node.Name = request.Name.Trim();
        node.TerritoryLevel = levelCode;
        node.CountryCode = request.CountryCode?.Trim();
        node.DivisionCode = request.DivisionCode?.Trim();
        node.RegionCode = request.RegionCode?.Trim();
        node.AreaCode = request.AreaCode?.Trim();
        node.ZoneCode = request.ZoneCode?.Trim();
        node.MicroZoneCode = request.MicroZoneCode?.Trim();
        node.EffectiveFrom = request.EffectiveFrom;
        node.EffectiveTo = request.EffectiveTo;
        node.SortOrder = request.SortOrder;
        node.MicroZoneProfile = CreateTerritoryNodeHandler.ToProfile(request.MicroZoneProfile);
        node.CorrelationId = request.CorrelationId?.Trim();
        node.UpdatedAt = DateTimeOffset.UtcNow;

        await _nodes.UpdateAsync(node, cancellationToken);
        return Response<bool>.Success(true);
    }
}
