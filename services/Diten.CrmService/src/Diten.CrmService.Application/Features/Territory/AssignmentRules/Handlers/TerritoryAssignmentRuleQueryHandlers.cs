using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules.Handlers;

public sealed class GetTerritoryAssignmentRuleListHandler
    : IRequestHandler<GetTerritoryAssignmentRuleListQuery, Response<TerritoryAssignmentRuleListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;

    public GetTerritoryAssignmentRuleListHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _rules = rules;
    }

    public async Task<Response<TerritoryAssignmentRuleListDto>> Handle(
        GetTerritoryAssignmentRuleListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryAssignmentRuleListDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryAssignmentRuleListDto>.Fail("Territory model not found.", 404);
        }

        var rules = await _rules.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken))
            .ToDictionary(n => n.Id);

        var items = rules
            .Select(r => TerritoryAssignmentRuleMapper.ToDto(r, nodes.GetValueOrDefault(r.TerritoryId)))
            .ToList();

        var isEditable = string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase);

        return Response<TerritoryAssignmentRuleListDto>.Success(new TerritoryAssignmentRuleListDto(
            model.Id, model.Status, isEditable, items.Count, items.Count(i => i.IsEnabled), items));
    }
}

public sealed class GetTerritoryAssignmentRuleByIdHandler
    : IRequestHandler<GetTerritoryAssignmentRuleByIdQuery, Response<TerritoryAssignmentRuleDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;

    public GetTerritoryAssignmentRuleByIdHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _rules = rules;
    }

    public async Task<Response<TerritoryAssignmentRuleDto>> Handle(
        GetTerritoryAssignmentRuleByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryAssignmentRuleDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryAssignmentRuleDto>.Fail("Territory model not found.", 404);
        }

        var rule = await _rules.GetByIdAsync(tenantId, request.ModelId, request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Response<TerritoryAssignmentRuleDto>.Fail("Assignment rule not found.", 404);
        }

        var node = await _nodes.GetByIdAsync(tenantId, request.ModelId, rule.TerritoryId, cancellationToken);
        return Response<TerritoryAssignmentRuleDto>.Success(TerritoryAssignmentRuleMapper.ToDto(rule, node));
    }
}
