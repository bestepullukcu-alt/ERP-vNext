using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.PlanVsCurrent.Handlers;

/// <summary>Shared plumbing for the three FU04B read surfaces. Every member is read-only.</summary>
public abstract class TerritoryPlanVsCurrentHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly ITerritoryModelRepository Models;
    protected readonly ITerritoryNodeRepository Nodes;
    protected readonly ITerritoryResourceAssignmentRepository Assignments;
    protected readonly ITerritoryResourceAssignmentPlanSnapshotRepository Snapshots;

    protected TerritoryPlanVsCurrentHandlerBase(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments,
        ITerritoryResourceAssignmentPlanSnapshotRepository snapshots)
    {
        Tenant = tenant;
        Models = models;
        Nodes = nodes;
        Assignments = assignments;
        Snapshots = snapshots;
    }

    /// <summary>Draft/never-activated → planning preview; activated but no baseline → not-captured; otherwise available.</summary>
    protected static string ResolveState(TerritoryModel model, TerritoryResourceAssignmentPlanSnapshot? snapshot)
    {
        if (snapshot is not null)
        {
            return TerritoryPlanVsCurrentStates.Available;
        }

        return string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase)
            ? TerritoryPlanVsCurrentStates.NotYetActivated
            : TerritoryPlanVsCurrentStates.NotCaptured;
    }

    /// <summary>Archived / inactive models still compare, but the view is explicitly historical (pack D-FU04B-6).</summary>
    protected static bool IsHistorical(TerritoryModel model)
        => !string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase);
}

public sealed class GetTerritoryResourceAssignmentPlanSnapshotHandler : TerritoryPlanVsCurrentHandlerBase,
    IRequestHandler<GetTerritoryResourceAssignmentPlanSnapshotQuery, Response<TerritoryPlanSnapshotDto>>
{
    public GetTerritoryResourceAssignmentPlanSnapshotHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments,
        ITerritoryResourceAssignmentPlanSnapshotRepository snapshots)
        : base(tenant, models, nodes, assignments, snapshots) { }

    public async Task<Response<TerritoryPlanSnapshotDto>> Handle(
        GetTerritoryResourceAssignmentPlanSnapshotQuery request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryPlanSnapshotDto>.Fail("Tenant context is required.", 400);
        }

        var model = await Models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryPlanSnapshotDto>.Fail("Territory model not found.", 404);
        }

        var versions = await Snapshots.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var snapshot = versions.Count == 0 ? null : versions[0];

        // A missing baseline is a STATE, never a 404 (pack §22.4 API behaviour).
        return Response<TerritoryPlanSnapshotDto>.Success(new TerritoryPlanSnapshotDto(
            model.Id, model.ModelCode, model.Name, model.Status,
            ResolveState(model, snapshot),
            snapshot?.Id, snapshot?.SnapshotVersion, snapshot?.CapturedAt, snapshot?.CapturedBy,
            snapshot?.ActivationCorrelationId,
            snapshot?.Lines.Count ?? 0,
            versions.Select(v => v.SnapshotVersion).ToList(),
            (snapshot?.Lines ?? []).Select(l => new TerritoryPlanSnapshotLineDto(
                l.TerritoryNodeId, l.TerritoryNodeCode, l.TerritoryNodeName, l.BusinessScopes,
                l.PositionCode, l.PositionTitle, l.PositionType,
                l.ResourceId, l.ResourceType, l.ResourceDisplayName,
                l.PlannedEffectiveFrom, l.PlannedEffectiveTo, l.IsPrimary, l.SourceAssignmentId)).ToList()));
    }
}

public sealed class GetTerritoryPlanVsCurrentHandler : TerritoryPlanVsCurrentHandlerBase,
    IRequestHandler<GetTerritoryPlanVsCurrentQuery, Response<TerritoryPlanVsCurrentDto>>
{
    public GetTerritoryPlanVsCurrentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments,
        ITerritoryResourceAssignmentPlanSnapshotRepository snapshots)
        : base(tenant, models, nodes, assignments, snapshots) { }

    public async Task<Response<TerritoryPlanVsCurrentDto>> Handle(
        GetTerritoryPlanVsCurrentQuery request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryPlanVsCurrentDto>.Fail("Tenant context is required.", 400);
        }

        var model = await Models.GetByIdAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryPlanVsCurrentDto>.Fail("Territory model not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var snapshot = await Snapshots.GetLatestAsync(tenantId, request.ModelId, cancellationToken);
        var state = ResolveState(model, snapshot);

        var assignments = await Assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await Nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);

        var rows = state == TerritoryPlanVsCurrentStates.Available
            ? TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
                model.Id, model.ModelCode, snapshot, assignments, nodes, effectiveAt))
            : [];
        rows = TerritoryPlanVsCurrentEngine.Filter(
            rows, request.TerritoryNodeId, request.BusinessUnit, request.PositionCode, request.ResourceId, request.DiffType);

        var currentCount = assignments.Count(a => TerritoryCurrentResponsibilityPolicy.IsCurrent(a, effectiveAt));

        return Response<TerritoryPlanVsCurrentDto>.Success(new TerritoryPlanVsCurrentDto(
            model.Id, model.ModelCode, model.Name, model.Status, state, IsHistorical(model),
            snapshot?.Id, snapshot?.SnapshotVersion, snapshot?.CapturedAt, snapshot?.CapturedBy,
            snapshot?.ActivationCorrelationId, effectiveAt,
            TerritoryPlanVsCurrentEngine.Summarize(snapshot?.Lines.Count ?? 0, currentCount, rows),
            rows));
    }
}

/// <summary>Person-level view: "where was this resource planned, where is it current?" across every model that
/// planned or currently holds it.</summary>
public sealed class GetResourcePlanVsCurrentHandler : TerritoryPlanVsCurrentHandlerBase,
    IRequestHandler<GetResourcePlanVsCurrentQuery, Response<ResourcePlanVsCurrentDto>>
{
    public GetResourcePlanVsCurrentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments,
        ITerritoryResourceAssignmentPlanSnapshotRepository snapshots)
        : base(tenant, models, nodes, assignments, snapshots) { }

    public async Task<Response<ResourcePlanVsCurrentDto>> Handle(
        GetResourcePlanVsCurrentQuery request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<ResourcePlanVsCurrentDto>.Fail("Tenant context is required.", 400);
        }
        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            return Response<ResourcePlanVsCurrentDto>.Fail("ResourceId is required.", 400);
        }

        var resourceId = request.ResourceId.Trim();
        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;

        var resourceAssignments = await Assignments.ListByResourceAsync(tenantId, resourceId, cancellationToken);
        var plannedSnapshots = await Snapshots.ListByResourceAsync(tenantId, resourceId, cancellationToken);

        var modelIds = resourceAssignments.Select(a => a.ModelId)
            .Concat(plannedSnapshots.Select(s => s.TerritoryModelId))
            .Distinct()
            .ToList();

        var rows = new List<TerritoryPlanVsCurrentRowDto>();
        var displayName = resourceAssignments.FirstOrDefault()?.Resource.DisplayName
                          ?? plannedSnapshots.SelectMany(s => s.Lines)
                              .FirstOrDefault(l => string.Equals(l.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                              ?.ResourceDisplayName
                          ?? resourceId;
        var plannedCount = 0;
        var currentCount = 0;

        foreach (var modelId in modelIds)
        {
            var model = await Models.GetByIdAsync(tenantId, modelId, cancellationToken);
            if (model is null)
            {
                continue;
            }

            var snapshot = await Snapshots.GetLatestAsync(tenantId, modelId, cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            var assignments = await Assignments.ListByModelAsync(tenantId, modelId, cancellationToken);
            var nodes = (await Nodes.ListByModelAsync(tenantId, modelId, cancellationToken)).ToDictionary(n => n.Id);

            var modelRows = TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
                model.Id, model.ModelCode, snapshot, assignments, nodes, effectiveAt));

            // Person view: keep only rows this resource appears in, on either side.
            rows.AddRange(TerritoryPlanVsCurrentEngine.Filter(
                modelRows, request.TerritoryNodeId, request.BusinessUnit, request.PositionCode, resourceId, request.DiffType));

            plannedCount += snapshot.Lines.Count(l => string.Equals(l.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase));
            currentCount += assignments.Count(a =>
                string.Equals(a.Resource.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase)
                && TerritoryCurrentResponsibilityPolicy.IsCurrent(a, effectiveAt));
        }

        var ordered = rows
            .OrderBy(r => TerritoryPlanVsCurrentDiffTypes.Rank(r.DiffType))
            .ThenBy(r => r.ModelCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.TerritoryNodeCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<ResourcePlanVsCurrentDto>.Success(new ResourcePlanVsCurrentDto(
            resourceId, displayName, effectiveAt,
            ordered.Select(r => r.ModelId).Distinct().Count(),
            TerritoryPlanVsCurrentEngine.Summarize(plannedCount, currentCount, ordered),
            ordered));
    }
}
