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
}
