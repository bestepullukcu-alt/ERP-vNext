using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments;

/// <summary>One current-coverage row for an account: the effective-now territory node plus its owning model's
/// country scope. Read projection only.</summary>
public sealed record AccountCurrentCoverage(
    Guid AccountId,
    Guid TerritoryNodeId,
    string TerritoryNodeCode,
    string TerritoryNodeName,
    string? CountryScope,
    string AssignmentStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

/// <summary>
/// Resolves the current (effective-now) MOD-0151 territory coverage for a set of accounts, running the shared
/// <see cref="TerritoryCoverageLifecyclePolicy"/> gate (assignment open AND owning model operationally valid). The
/// effective window is filtered in memory so DateTimeOffset (BSON array) never enters a Mongo range filter.
/// Used by the Account grid, the Contact grid, and Contact 360 (contact coverage is derived from its linked accounts).
/// </summary>
public static class AccountCurrentCoverageResolver
{
    public static async Task<IReadOnlyList<AccountCurrentCoverage>> ResolveAsync(
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryModelRepository models,
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (accountIds is null || accountIds.Count == 0) return [];

        var active = await assignments.ListActiveByAccountIdsAsync(tenantId, accountIds, cancellationToken);
        var open = active.Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, at)).ToList();
        if (open.Count == 0) return [];

        var modelDict = (await models.ListByIdsAsync(
                tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(open), cancellationToken))
            .ToDictionary(m => m.Id);

        return TerritoryCoverageLifecyclePolicy.FilterCurrent(open, modelDict, at)
            .Select(a => new AccountCurrentCoverage(
                a.AccountId, a.TerritoryNodeId, a.TerritoryNodeCode, a.TerritoryNodeName,
                modelDict.GetValueOrDefault(a.TerritoryModelId)?.CountryScope,
                a.AssignmentStatus, a.EffectiveFrom, a.EffectiveTo))
            .ToList();
    }

    /// <summary>Accounts whose CURRENT coverage (both gates pass at <paramref name="at"/>) is on one of
    /// <paramref name="nodeIds"/> — the Accounts-grid Territory Node filter. The candidate active assignments are
    /// narrowed in Mongo by node id, then the shared <see cref="TerritoryCoverageLifecyclePolicy"/> gate is applied in
    /// memory (assignment window AND owning-model validity), so the effective-window never enters a Mongo range filter
    /// and the gate is never reimplemented.</summary>
    public static async Task<HashSet<Guid>> ResolveCoveredAccountIdsByNodesAsync(
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryModelRepository models,
        Guid tenantId,
        IReadOnlyCollection<Guid> nodeIds,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (nodeIds is null || nodeIds.Count == 0) return [];

        var candidates = await assignments.ListActiveByNodesAsync(tenantId, nodeIds, cancellationToken);
        var open = candidates.Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, at)).ToList();
        if (open.Count == 0) return [];

        var modelDict = (await models.ListByIdsAsync(
                tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(open), cancellationToken))
            .ToDictionary(m => m.Id);

        return TerritoryCoverageLifecyclePolicy.FilterCurrent(open, modelDict, at)
            .Select(a => a.AccountId)
            .ToHashSet();
    }

    /// <summary>Accounts whose CURRENT coverage (both gates pass at <paramref name="at"/>) is on a model with one of
    /// <paramref name="countryScopes"/> — the Accounts-grid Country Scope filter. The owning models are pre-resolved
    /// from the scope, their active assignments narrowed in Mongo by model id, then the shared
    /// <see cref="TerritoryCoverageLifecyclePolicy"/> gate is applied in memory over that same model set (so a
    /// deactivated / expired / soft-deleted model contributes nothing, exactly as in the grid column).</summary>
    public static async Task<HashSet<Guid>> ResolveCoveredAccountIdsByCountryScopesAsync(
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryModelRepository models,
        Guid tenantId,
        IReadOnlyCollection<string> countryScopes,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (countryScopes is null || countryScopes.Count == 0) return [];

        var scopedModels = await models.ListByCountryScopesAsync(tenantId, countryScopes, cancellationToken);
        if (scopedModels.Count == 0) return [];
        var modelDict = scopedModels.ToDictionary(m => m.Id);

        var candidates = await assignments.ListActiveByModelIdsAsync(
            tenantId, modelDict.Keys.ToList(), cancellationToken);
        var open = candidates.Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, at)).ToList();
        if (open.Count == 0) return [];

        return TerritoryCoverageLifecyclePolicy.FilterCurrent(open, modelDict, at)
            .Select(a => a.AccountId)
            .ToHashSet();
    }
}
