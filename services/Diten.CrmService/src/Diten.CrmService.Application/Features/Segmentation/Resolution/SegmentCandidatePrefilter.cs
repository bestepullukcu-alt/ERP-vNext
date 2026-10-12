using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// WP-E2E-FIX-3 (E1-B2) — narrows the candidate query for the conditions that do NOT live on the subject document:
/// <c>territory.node / territory.model / territory.has-coverage = true</c> (through the existing MOD-0151 current-coverage
/// resolver — the same gates the evaluator's coverage uses) and <c>contact.account-role / is-primary / account-type</c>
/// (an account–contact link pre-query). Each answers "which subjects can possibly satisfy this leaf" as an id set; the
/// candidate source turns it into <c>_id IN</c>.
/// <para><b>Superset only.</b> Every set is a SUPERSET of the subjects the evaluator will accept for that leaf (link
/// status is not filtered, a deleted account's links are kept…), and the in-memory evaluation is unchanged, so the
/// member list cannot change — only the candidate set shrinks. A set over <see cref="SegmentPushdownRules.MaxPrefilterIds"/>
/// is dropped (the leaf is simply not narrowed, today's path), and so is a territory leaf while the tenant has no
/// operationally valid model (the evaluator then eliminates with its own reason; nothing is pre-judged here).</para>
/// <para>Reads only; bounded: at most two bulk reads per pushable leaf, never one per candidate.</para>
/// </summary>
public sealed class SegmentCandidatePrefilter
{
    private readonly ISegmentCandidateSource _source;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryModelRepository _models;

    public SegmentCandidatePrefilter(
        ISegmentCandidateSource source,
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryModelRepository models)
    {
        _source = source;
        _assignments = assignments;
        _models = models;
    }

    /// <summary>NodeId → the subject ids that can satisfy that leaf. Leaves that cannot be narrowed are absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> BuildAsync(
        Guid tenantId, Segment segment, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var isContact = string.Equals(
            SegmentSubjectTypes.Normalize(segment.SubjectType), SegmentSubjectTypes.Contact, StringComparison.Ordinal);

        foreach (var leaf in SegmentPushdownRules.PrefilterLeaves(segment.Criteria, segment.MatchMode, isContact))
        {
            var ids = await ResolveLeafAsync(tenantId, leaf, isContact, effectiveAt, cancellationToken);
            if (ids is not null)
            {
                result[leaf.NodeId] = ids;
            }
        }

        return result;
    }

    private async Task<IReadOnlyCollection<Guid>?> ResolveLeafAsync(
        Guid tenantId, SegmentCriteriaNode leaf, bool isContact, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var code = (leaf.AttributeCode ?? string.Empty).Trim().ToLowerInvariant();
        if (code is SegmentAttributeCatalog.ContactAccountRole
            or SegmentAttributeCatalog.ContactAccountType
            or SegmentAttributeCatalog.ContactIsPrimary)
        {
            return await _source.ListContactIdsByLinkAsync(
                tenantId, leaf, SegmentPushdownRules.MaxPrefilterIds, cancellationToken);
        }

        var accounts = await CoveredAccountsAsync(tenantId, code, leaf, at, cancellationToken);
        if (accounts is null || accounts.Count > SegmentPushdownRules.MaxPrefilterIds)
        {
            return null;
        }

        return isContact
            ? await _source.ListContactIdsLinkedToAccountsAsync(
                tenantId, accounts, SegmentPushdownRules.MaxPrefilterIds, cancellationToken)
            : accounts;
    }

    /// <summary>The accounts whose CURRENT coverage satisfies a territory leaf, or null when coverage is unavailable
    /// (no operationally valid model — the evaluator answers that case itself).</summary>
    private async Task<IReadOnlyCollection<Guid>?> CoveredAccountsAsync(
        Guid tenantId, string code, SegmentCriteriaNode leaf, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var activeModels = await _models.ListActiveAsync(tenantId, Guid.Empty, cancellationToken);
        if (!activeModels.Any(m => TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, at)))
        {
            return null;
        }

        var ids = SegmentPushdownRules.Values(leaf)
            .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        if (SegmentOperators.Normalize(leaf.Operator) == SegmentOperators.Eq)
        {
            ids = ids.Take(1).ToList(); // the evaluator compares eq against the FIRST value only
        }

        switch (code)
        {
            case SegmentAttributeCatalog.TerritoryNode:
                return ids.Count == 0
                    ? Array.Empty<Guid>()
                    : await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
                        _assignments, _models, tenantId, ids, at, cancellationToken);

            case SegmentAttributeCatalog.TerritoryModel:
                return ids.Count == 0 ? Array.Empty<Guid>() : await CoveredByModelsAsync(tenantId, ids, at, cancellationToken);

            case SegmentAttributeCatalog.TerritoryHasCoverage:
                return await CoveredByModelsAsync(
                    tenantId, activeModels.Select(m => m.Id).ToList(), at, cancellationToken);

            default:
                return null;
        }
    }

    private async Task<IReadOnlyCollection<Guid>> CoveredByModelsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> modelIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var open = (await _assignments.ListActiveByModelIdsAsync(tenantId, modelIds, cancellationToken))
            .Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, at))
            .ToList();
        if (open.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var modelDict = (await _models.ListByIdsAsync(
                tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(open), cancellationToken))
            .ToDictionary(m => m.Id);
        return TerritoryCoverageLifecyclePolicy.FilterCurrent(open, modelDict, at).Select(a => a.AccountId).ToHashSet();
    }
}
