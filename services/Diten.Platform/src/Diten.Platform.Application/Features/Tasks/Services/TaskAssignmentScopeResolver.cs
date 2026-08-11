using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-057 — WHO may receive work from me. ONE rule, consumed by the people picker and by the pool picker.
///
/// <para><b>The defect this closes.</b> Both pickers called <c>GetAllAsync</c> on assignments, positions and
/// units and filtered on nothing but dates and archive flags. A user in Miguel Garriga saw — and could assign
/// work to — every employee of Grand Medical Poland and Turkey. Listing was right by accident (position
/// ownership narrowed it); assignment was not right even by accident. Poland is inside the EU/GDPR and Turkey is
/// not, so the boundary is legal, not cosmetic.</para>
///
/// <para><b>The rule, three legs.</b> A candidate is in scope when they are
/// (1) in the SAME legal entity as me, OR
/// (2) BELOW me in the position reporting chain, OR
/// (3) inside an org-unit/position scope explicitly granted to me.
/// Modelled on Oracle Fusion's security profiles — Organization Security Profile (1), Person Security Profile
/// "Manager Hierarchy" (2), "Custom/List" (3) — and on SAP's Structural Authorization.</para>
///
/// <para><b>What this class deliberately does NOT do.</b> It does not compute scopes. MOD-0018-FU15's
/// <see cref="IDataScopeResolver"/> already turns the org master data into
/// <see cref="EntitlementDataScopeKind.OrgUnit"/> / <c>Position</c> / <c>ManagerChain</c> / <c>LegalEntity</c>
/// scopes, cycle-safe and fail-closed, and it is registered. Building a second engine beside it would be the
/// K6 defect: two places to disagree about the same truth. This class TRANSLATES those scopes into the one
/// question the pickers ask — "may I hand work to the holder of this position?".</para>
///
/// <para><b>⚠ MEASURED GAP, and why one walk still happens here.</b> The resolver emits the chain in the
/// direction it was built for: <c>ManagerChain</c> holds the positions ABOVE me (my managers), because data
/// scoping asks "whose rows may I see through my superiors". Assignment asks the OPPOSITE — who is below me.
/// The resolver emits no downward scope kind, so the descent is derived here from the SAME field
/// (<see cref="Position.ReportsToPositionId"/>) with the SAME guards the resolver uses (cycle set, depth 32) by
/// walking UP from each candidate and testing whether it meets one of MY positions — and "my positions" comes
/// from the resolver's own <c>Position</c> scopes rather than from a second query. Recorded in BL-057; if the
/// resolver ever gains a downward kind, this walk is what gets deleted.</para>
/// </summary>
public interface ITaskAssignmentScopeResolver
{
    /// <summary>
    /// The scope in force for the current actor. Resolved once per request by the caller and then asked many
    /// times — a picker judges every candidate row against it.
    /// </summary>
    Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct);
}

/// <summary>
/// A resolved, immutable answer to "who may I hand work to". Built by <see cref="TaskAssignmentScopeResolver"/>
/// and then queried per candidate; it holds no repositories and performs no I/O.
/// </summary>
public sealed class TaskAssignmentScope
{
    private readonly HashSet<Guid> _legalEntityIds;
    private readonly HashSet<Guid> _orgUnitIds;
    private readonly HashSet<Guid> _positionIds;
    private readonly HashSet<Guid> _subordinatePositionIds;

    internal TaskAssignmentScope(
        HashSet<Guid> legalEntityIds,
        HashSet<Guid> orgUnitIds,
        HashSet<Guid> positionIds,
        HashSet<Guid> subordinatePositionIds)
    {
        _legalEntityIds = legalEntityIds;
        _orgUnitIds = orgUnitIds;
        _positionIds = positionIds;
        _subordinatePositionIds = subordinatePositionIds;
    }

    /// <summary>
    /// Nothing resolved. FAIL-CLOSED: every candidate is out of scope, which is the resolver's own rule for a
    /// user with no active position assignment. The picker must report the count rather than render a bare empty
    /// list (BL-072) — an empty list with no explanation is the defect, not the emptiness.
    /// </summary>
    public static TaskAssignmentScope Empty { get; } = new([], [], [], []);

    /// <summary>True when the actor has no scope at all — used to explain an empty picker.</summary>
    public bool IsEmpty
        => _legalEntityIds.Count == 0 && _orgUnitIds.Count == 0 && _subordinatePositionIds.Count == 0;

    /// <summary>
    /// May work be handed to the holder of this position?
    ///
    /// <para>Leg (2) is checked through <paramref name="positionId"/> rather than through the unit, because the
    /// reporting chain is allowed to CROSS the company boundary while the unit tree is not — that asymmetry is
    /// deliberate (unit tree = financial/legal truth, position chain = authority truth) and this rule uses it
    /// rather than changing it.</para>
    /// </summary>
    public bool Allows(Guid positionId, Guid organizationUnitId, Guid legalEntityId)
        => _legalEntityIds.Contains(legalEntityId)          // (1) same company
           || _subordinatePositionIds.Contains(positionId)  // (2) below me, company boundary or not
           || _orgUnitIds.Contains(organizationUnitId)      // (3) an org unit granted to me
           || _positionIds.Contains(positionId);            // (3) a position granted to me
}

/// <inheritdoc cref="ITaskAssignmentScopeResolver"/>
public sealed class TaskAssignmentScopeResolver : ITaskAssignmentScopeResolver
{
    /// <summary>The module the scope is asked for. Same code the manifest declares.</summary>
    private const string ModuleCode = "tasks";

    /// <summary>Mirrors <c>OrgDataScopeResolver</c> and <c>GetManagerChainQuery</c>; not re-chosen here.</summary>
    private const int MaxChainDepth = 32;

    private readonly IDataScopeResolver _dataScopes;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public TaskAssignmentScopeResolver(
        IDataScopeResolver dataScopes,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _dataScopes = dataScopes;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct)
    {
        var scopes = await _dataScopes.ResolveAsync(
            _tenantContext.TenantId, _currentUser.UserId, ModuleCode, featureCode: null, ct);

        if (scopes.Count == 0)
        {
            return TaskAssignmentScope.Empty;
        }

        var legalEntityIds = Ids(scopes, EntitlementDataScopeKind.LegalEntity);
        var orgUnitIds = Ids(scopes, EntitlementDataScopeKind.OrgUnit);
        var positionIds = Ids(scopes, EntitlementDataScopeKind.Position);

        // ManagerChain is deliberately NOT read here: it points upward (my managers) and assignability points
        // downward. Reading it would make every subordinate able to assign work to their own boss.
        var subordinates = await ResolveSubordinatePositionsAsync(positionIds, ct);

        return new TaskAssignmentScope(legalEntityIds, orgUnitIds, positionIds, subordinates);
    }

    private static HashSet<Guid> Ids(IReadOnlyList<EntitlementDataScope> scopes, EntitlementDataScopeKind kind)
        => scopes
            .Where(scope => scope.Kind == kind && scope.IsInclude && scope.ScopeId is not null)
            .Select(scope => scope.ScopeId!.Value)
            .ToHashSet();

    /// <summary>
    /// Every position that reaches one of MINE by walking up its reporting chain.
    ///
    /// <para>Walked once over the whole tenant rather than per candidate: the picker judges every row, so a
    /// per-row walk would re-traverse the same chains N times. Each walk carries its own visited set (cycle
    /// guard) and stops at <see cref="MaxChainDepth"/>, exactly as <c>GetManagerChainQueryHandler</c> and
    /// <c>OrgDataScopeResolver</c> do — a corrupt chain must degrade to "not in scope", never to a hang.</para>
    /// </summary>
    private async Task<HashSet<Guid>> ResolveSubordinatePositionsAsync(
        IReadOnlySet<Guid> myPositionIds, CancellationToken ct)
    {
        var subordinates = new HashSet<Guid>();
        if (myPositionIds.Count == 0)
        {
            return subordinates;
        }

        var all = await _positions.GetAllAsync(ct);
        var byId = all.ToDictionary(position => position.Id);

        foreach (var candidate in all)
        {
            if (candidate.IsArchived || myPositionIds.Contains(candidate.Id))
            {
                continue;
            }

            var visited = new HashSet<Guid> { candidate.Id };
            var cursor = candidate.ReportsToPositionId;

            for (var depth = 1; cursor.HasValue && depth <= MaxChainDepth; depth++)
            {
                if (!visited.Add(cursor.Value))
                {
                    break;   // cycle: this branch answers "no", it does not throw.
                }

                if (myPositionIds.Contains(cursor.Value))
                {
                    subordinates.Add(candidate.Id);
                    break;
                }

                if (!byId.TryGetValue(cursor.Value, out var manager) || manager.IsArchived)
                {
                    break;
                }

                cursor = manager.ReportsToPositionId;
            }
        }

        return subordinates;
    }

    /// <summary>
    /// The legal entity a position belongs to, read from its organization unit. Exposed as a helper because both
    /// pickers need the same join and the unit is where <c>LegalEntityId</c> actually lives.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, OrganizationUnit>> LoadUnitsAsync(CancellationToken ct)
        => (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);
}
