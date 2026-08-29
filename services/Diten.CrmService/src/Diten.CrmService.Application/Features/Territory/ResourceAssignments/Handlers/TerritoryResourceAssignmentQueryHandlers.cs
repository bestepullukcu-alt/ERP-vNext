using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments.Handlers;

public sealed class GetTerritoryResourceAssignmentListHandler
    : IRequestHandler<GetTerritoryResourceAssignmentListQuery, Response<TerritoryResourceAssignmentListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public GetTerritoryResourceAssignmentListHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _assignments = assignments;
    }

    public async Task<Response<TerritoryResourceAssignmentListDto>> Handle(
        GetTerritoryResourceAssignmentListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryResourceAssignmentListDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryResourceAssignmentListDto>.Fail("Territory model not found.", 404);
        }

        var assignments = await _assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var now = DateTimeOffset.UtcNow;

        var items = assignments
            .Select(a => TerritoryResourceAssignmentMapper.ToDto(
                a, a.TerritoryId is { } id ? nodes.GetValueOrDefault(id) : null, now))
            .ToList();

        var isEditable = string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase);
        var modelScopes = model.BusinessScopes
            .Where(s => string.Equals(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ScopeCode)
            .ToList();

        return Response<TerritoryResourceAssignmentListDto>.Success(new TerritoryResourceAssignmentListDto(
            model.Id, model.Status, isEditable, items.Count,
            items.Count(i => string.Equals(i.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)),
            modelScopes, items));
    }
}

public sealed class GetCurrentTerritoryResourceResponsibilitiesHandler
    : IRequestHandler<GetCurrentTerritoryResourceResponsibilitiesQuery, Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public GetCurrentTerritoryResourceResponsibilitiesHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
        => (_tenant, _models, _nodes, _assignments) = (tenant, models, nodes, assignments);

    public async Task<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>> Handle(
        GetCurrentTerritoryResourceResponsibilitiesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("Tenant context is required.", 400);
        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("Territory model not found.", 404);
        if (!string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Success([]);

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var items = (await _assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken))
            .Where(a => string.Equals(a.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)
                        && a.ValidFrom <= effectiveAt && (a.ValidTo is null || a.ValidTo >= effectiveAt)
                        && (request.TerritoryId is null || a.TerritoryId == request.TerritoryId)
                        && (!request.PrimaryOnly || a.IsPrimary)
                        && (string.IsNullOrWhiteSpace(request.PositionCode)
                            || string.Equals(a.EffectivePositionCode, request.PositionCode, StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrWhiteSpace(request.BusinessUnitScope)
                            || a.BusinessScopes.Any(s => string.Equals(
                                s.ScopeCode, request.BusinessUnitScope, StringComparison.OrdinalIgnoreCase))))
            .Select(a => TerritoryResourceAssignmentMapper.ToDto(
                a, a.TerritoryId is { } id ? nodes.GetValueOrDefault(id) : null, effectiveAt))
            .ToList();
        return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Success(items);
    }
}

public sealed class GetTerritoryResourceAssignmentHistoryHandler
    : IRequestHandler<GetTerritoryResourceAssignmentHistoryQuery, Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public GetTerritoryResourceAssignmentHistoryHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
        => (_tenant, _models, _nodes, _assignments) = (tenant, models, nodes, assignments);

    public async Task<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>> Handle(
        GetTerritoryResourceAssignmentHistoryQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("Tenant context is required.", 400);
        if (await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken) is null)
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("Territory model not found.", 404);
        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var now = DateTimeOffset.UtcNow;
        var items = (await _assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken))
            .Where(a => (request.TerritoryId is null || a.TerritoryId == request.TerritoryId)
                        && (string.IsNullOrWhiteSpace(request.ResourceId)
                            || string.Equals(a.Resource.ResourceId, request.ResourceId, StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrWhiteSpace(request.PositionCode)
                            || string.Equals(a.EffectivePositionCode, request.PositionCode, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.ValidFrom)
            .Select(a => TerritoryResourceAssignmentMapper.ToDto(
                a, a.TerritoryId is { } id ? nodes.GetValueOrDefault(id) : null, now))
            .ToList();
        return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Success(items);
    }
}

public sealed class GetResourceTerritoryResponsibilitiesHandler
    : IRequestHandler<GetResourceTerritoryResponsibilitiesQuery, Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public GetResourceTerritoryResponsibilitiesHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
        => (_tenant, _models, _nodes, _assignments) = (tenant, models, nodes, assignments);

    public async Task<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>> Handle(
        GetResourceTerritoryResponsibilitiesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("Tenant context is required.", 400);
        if (string.IsNullOrWhiteSpace(request.ResourceId))
            return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Fail("ResourceId is required.", 400);

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var result = new List<TerritoryResourceAssignmentDto>();
        foreach (var assignment in await _assignments.ListByResourceAsync(tenantId, request.ResourceId.Trim(), cancellationToken))
        {
            var model = await _models.GetByIdAsync(tenantId, assignment.ModelId, cancellationToken);
            if (model is null) continue;
            if (!request.IncludeHistory
                && (!string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(assignment.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)
                    || assignment.ValidFrom > effectiveAt || (assignment.ValidTo is { } end && end < effectiveAt)))
                continue;
            var node = assignment.TerritoryId is { } nodeId
                ? await _nodes.GetByIdAsync(tenantId, assignment.ModelId, nodeId, cancellationToken) : null;
            result.Add(TerritoryResourceAssignmentMapper.ToDto(assignment, node, effectiveAt));
        }
        return Response<IReadOnlyList<TerritoryResourceAssignmentDto>>.Success(result);
    }
}

public sealed class GetTerritoryResourceAssignmentByIdHandler
    : IRequestHandler<GetTerritoryResourceAssignmentByIdQuery, Response<TerritoryResourceAssignmentDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public GetTerritoryResourceAssignmentByIdHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _assignments = assignments;
    }

    public async Task<Response<TerritoryResourceAssignmentDto>> Handle(
        GetTerritoryResourceAssignmentByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryResourceAssignmentDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryResourceAssignmentDto>.Fail("Territory model not found.", 404);
        }

        var assignment = await _assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Response<TerritoryResourceAssignmentDto>.Fail("Resource assignment not found.", 404);
        }

        var node = assignment.TerritoryId is { } nodeId
            ? await _nodes.GetByIdAsync(tenantId, request.ModelId, nodeId, cancellationToken)
            : null;

        return Response<TerritoryResourceAssignmentDto>.Success(
            TerritoryResourceAssignmentMapper.ToDto(assignment, node, DateTimeOffset.UtcNow));
    }
}

/// <summary>Read-only conflict report. Runs the same exclusivity engine the write path uses, but reports everything
/// at once (including advisory warnings) and writes nothing.</summary>
public sealed class ValidateTerritoryResourceConflictsHandler
    : IRequestHandler<ValidateTerritoryResourceConflictsCommand, Response<TerritoryResourceConflictReportDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public ValidateTerritoryResourceConflictsHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _assignments = assignments;
    }

    public async Task<Response<TerritoryResourceConflictReportDto>> Handle(
        ValidateTerritoryResourceConflictsCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryResourceConflictReportDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryResourceConflictReportDto>.Fail("Territory model not found.", 404);
        }

        var assignments = await _assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await _nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var (conflicts, warnings) = TerritoryResourceConflictEngine.Report(assignments, nodes);

        return Response<TerritoryResourceConflictReportDto>.Success(new TerritoryResourceConflictReportDto(
            model.Id, DateTimeOffset.UtcNow, assignments.Count, conflicts.Count, warnings.Count, conflicts, warnings));
    }
}
