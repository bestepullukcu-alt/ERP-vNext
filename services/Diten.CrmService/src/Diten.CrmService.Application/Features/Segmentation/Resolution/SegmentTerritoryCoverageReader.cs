using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 territory adapter. It calls the MOD-0151 <c>AccountCurrentCoverageResolver</c> <b>as it is</b> — one
/// bulk call for the entire candidate set — and copies nothing: no territory aggregate is read into a segment field,
/// no assignment is created, and no MOD-0151 file or signature changes.
/// <para>The one judgement it adds is the distinction MOD-0151-FU05A already established: is there an operationally
/// valid model at this instant at all? Without one, coverage is UNAVAILABLE (candidates with a territory criterion are
/// eliminated with a reason, the resolution completes). With one, an account that simply has no assignment is a
/// truthful "no coverage". A default is invented in neither case.</para>
/// </summary>
public sealed class SegmentTerritoryCoverageReader : ISegmentTerritoryCoverageReader
{
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryModelRepository _models;

    public SegmentTerritoryCoverageReader(
        IAccountTerritoryAssignmentRepository assignments, ITerritoryModelRepository models)
    {
        _assignments = assignments;
        _models = models;
    }

    public async Task<SegmentCoverageLoad> LoadAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var activeModels = await _models.ListActiveAsync(tenantId, Guid.Empty, cancellationToken);
        if (!activeModels.Any(m => TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, effectiveAt)))
        {
            return SegmentCoverageLoad.Unavailable;
        }

        if (accountIds.Count == 0)
        {
            return new SegmentCoverageLoad(true, Array.Empty<SegmentCoverageProjection>());
        }

        var coverage = await AccountCurrentCoverageResolver.ResolveAsync(
            _assignments, _models, tenantId, accountIds, effectiveAt, cancellationToken);

        var modelByNode = await MapModelsAsync(tenantId, accountIds, effectiveAt, cancellationToken);

        return new SegmentCoverageLoad(
            true,
            coverage
                .Select(c => new SegmentCoverageProjection(
                    c.AccountId,
                    c.TerritoryNodeId,
                    modelByNode.GetValueOrDefault((c.AccountId, c.TerritoryNodeId))))
                .ToList());
    }

    /// <summary>The MOD-0151 coverage projection intentionally exposes the node, not the model id, so the model is
    /// recovered from the same active assignment rows (one further bulk read, never one per candidate).</summary>
    private async Task<Dictionary<(Guid AccountId, Guid NodeId), Guid>> MapModelsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var assignments = await _assignments.ListActiveByAccountIdsAsync(tenantId, accountIds, cancellationToken);
        var map = new Dictionary<(Guid, Guid), Guid>();
        foreach (var assignment in assignments.Where(a =>
                     TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, effectiveAt)))
        {
            map[(assignment.AccountId, assignment.TerritoryNodeId)] = assignment.TerritoryModelId;
        }

        return map;
    }
}
