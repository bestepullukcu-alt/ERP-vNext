using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — the territory gate on account selection (§4.1 ②, D-TERRITORY-GATE = B, LOCKED). It reads MOD-0151
/// <see cref="IAccountTerritoryAssignmentRepository"/> and <b>WARNS</b> on an out-of-territory account — it is NEVER a
/// hard filter: an account with no active territory assignment is still selectable (matching the excluded-not-dropped
/// pattern; hiding it would hide a valid target). It writes nothing and reads read-only.
/// </summary>
public sealed class TerritoryGate
{
    private readonly ITenantContext _tenant;
    private readonly IAccountTerritoryAssignmentRepository _assignments;

    public TerritoryGate(ITenantContext tenant, IAccountTerritoryAssignmentRepository assignments)
    {
        _tenant = tenant;
        _assignments = assignments;
    }

    /// <summary>For each selected account, whether it currently carries an active territory assignment. Accounts that do
    /// not are returned as WARNINGS (out-of-territory); the caller surfaces them and still lets the plan proceed.</summary>
    public async Task<IReadOnlyList<TerritoryWarning>> WarnAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || accountIds is null || accountIds.Count == 0)
        {
            return Array.Empty<TerritoryWarning>();
        }

        var assigned = await _assignments.ListActiveByAccountIdsAsync(tenantId, accountIds, cancellationToken);
        var covered = assigned.Select(a => a.AccountId).ToHashSet();

        return accountIds
            .Where(id => id != Guid.Empty && !covered.Contains(id))
            .Distinct()
            .Select(id => new TerritoryWarning(id, "account_out_of_territory"))
            .ToList();
    }
}

/// <summary>One out-of-territory account warning. Advisory only — never a hard block (D-TERRITORY-GATE = B).</summary>
public sealed record TerritoryWarning(Guid AccountId, string Reason);
