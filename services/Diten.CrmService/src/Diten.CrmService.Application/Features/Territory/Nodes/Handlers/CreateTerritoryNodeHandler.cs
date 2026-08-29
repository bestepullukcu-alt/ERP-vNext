using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes.Handlers;

public sealed class CreateTerritoryNodeHandler : IRequestHandler<CreateTerritoryNodeCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryReferenceValidator _references;

    public CreateTerritoryNodeHandler(
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

    public async Task<Response<Guid>> Handle(CreateTerritoryNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<Guid>.Fail("Territory model not found.", 404);
        }

        if (!string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<Guid>.Fail("Nodes can only be added to a draft territory model.", 409);
        }

        TerritoryNode? parent = null;
        if (request.ParentTerritoryId is { } parentId)
        {
            parent = await _nodes.GetByIdAsync(tenantId, request.ModelId, parentId, cancellationToken);
            if (parent is null)
            {
                return Response<Guid>.Fail("Parent node not found in this model.", 404);
            }
        }

        var levelCode = request.TerritoryLevel.Trim();
        var validationError = await TerritoryNodeValidation.ValidateAsync(
            _references, model, parent, levelCode, TerritoryReferenceSets.DraftStatus,
            request.EffectiveFrom, request.EffectiveTo, request.MicroZoneProfile is not null, cancellationToken);
        if (validationError is not null)
        {
            return Response<Guid>.Fail(validationError.Message, validationError.StatusCode);
        }

        var territoryCode = request.TerritoryCode.Trim();
        if (await _nodes.ExistsByCodeAsync(tenantId, request.ModelId, territoryCode, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("TerritoryCode already exists in this model.", 409);
        }

        var node = new TerritoryNode
        {
            TenantId = tenantId,
            ModelId = request.ModelId,
            ParentTerritoryId = request.ParentTerritoryId,
            TerritoryCode = territoryCode,
            Name = request.Name.Trim(),
            TerritoryLevel = levelCode,
            CountryCode = request.CountryCode?.Trim(),
            DivisionCode = request.DivisionCode?.Trim(),
            RegionCode = request.RegionCode?.Trim(),
            AreaCode = request.AreaCode?.Trim(),
            ZoneCode = request.ZoneCode?.Trim(),
            MicroZoneCode = request.MicroZoneCode?.Trim(),
            Status = TerritoryReferenceSets.DraftStatus,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            SortOrder = request.SortOrder,
            MicroZoneProfile = ToProfile(request.MicroZoneProfile),
            CorrelationId = request.CorrelationId?.Trim()
        };

        await _nodes.InsertAsync(node, cancellationToken);
        return Response<Guid>.Success(node.Id, 201);
    }

    internal static MicroZoneProfile? ToProfile(MicroZoneProfileInput? input)
        => input is null ? null : new MicroZoneProfile
        {
            AnchorAccountId = input.AnchorAccountId,
            ClusterNotes = input.ClusterNotes?.Trim(),
            PlanningCenterType = input.PlanningCenterType?.Trim()
        };
}
