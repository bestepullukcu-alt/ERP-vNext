using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Authorization;

/// <summary>
/// MOD-0018-FU15 — Real <see cref="IDataScopeResolver"/>.
/// Computes the set of data scopes (the rows a user may see/act on) from the MOD-0288 organization master data:
/// Organization Unit tree, Position, effective-dated Position Assignment, and the derived Position reporting chain.
///
/// v1 emits exactly four scope kinds (locked by the module pack): <see cref="EntitlementDataScopeKind.OrgUnit"/>
/// (own + subtree, pre-expanded into a flat list), <see cref="EntitlementDataScopeKind.Position"/>,
/// <see cref="EntitlementDataScopeKind.ManagerChain"/> (Position IDs up the reporting chain), and
/// <see cref="EntitlementDataScopeKind.LegalEntity"/> (read-only MOD-0220 reference already stored on the Org Unit).
///
/// Role (action permission) and data scope (accessible rows) stay orthogonal: this resolver never produces an
/// allow/deny decision and never reads or infers permissions from Position. It is fail-closed — no/expired/invalid
/// Position Assignment yields no scope (zero rows for an opted-in module), and missing/archived nodes are skipped.
/// The FU10a contract signature is consumed unchanged; the FU12 once-per-request + memoized + fail-safe call shape
/// is preserved (FU12 swallows any exception thrown here and keeps the org fields empty).
/// </summary>
public sealed class OrgDataScopeResolver : IDataScopeResolver
{
    private const int MaxManagerChainDepth = 32;

    private readonly IOrganizationUnitRepository organizationUnits;
    private readonly IPositionRepository positions;
    private readonly IPositionAssignmentRepository positionAssignments;
    private readonly ILegalEntityReferenceValidator legalEntityValidator;

    public OrgDataScopeResolver(
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions,
        IPositionAssignmentRepository positionAssignments,
        ILegalEntityReferenceValidator legalEntityValidator)
    {
        this.organizationUnits = organizationUnits ?? throw new ArgumentNullException(nameof(organizationUnits));
        this.positions = positions ?? throw new ArgumentNullException(nameof(positions));
        this.positionAssignments = positionAssignments ?? throw new ArgumentNullException(nameof(positionAssignments));
        this.legalEntityValidator = legalEntityValidator ?? throw new ArgumentNullException(nameof(legalEntityValidator));
    }

    public async Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
        Guid tenantId,
        Guid userId,
        string moduleCode,
        string? featureCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fail-closed: no identifiable user → no scope.
        if (userId == Guid.Empty)
        {
            return Array.Empty<EntitlementDataScope>();
        }

        var now = DateTimeOffset.UtcNow;

        // Active, effective-dated assignments for this user: interval is [EffectiveFrom, EffectiveTo).
        // Repository reads are already tenant-scoped + non-deleted; tenantId is re-checked as defense in depth.
        var allAssignments = await positionAssignments.GetAllAsync(cancellationToken);
        var activePositionIds = allAssignments
            .Where(a => a.TenantId == tenantId
                && a.UserId == userId
                && a.EffectiveFrom <= now
                && (a.EffectiveTo == null || a.EffectiveTo > now))
            .Select(a => a.PositionId)
            .Distinct()
            .ToList();

        // Fail-closed: no active assignment → no scope.
        if (activePositionIds.Count == 0)
        {
            return Array.Empty<EntitlementDataScope>();
        }

        // Resolve the active positions (skip missing/archived).
        var activePositions = new List<Position>();
        foreach (var positionId in activePositionIds)
        {
            var position = await positions.GetByIdAsync(positionId, cancellationToken);
            if (position is not null && position.TenantId == tenantId && !position.IsArchived)
            {
                activePositions.Add(position);
            }
        }

        if (activePositions.Count == 0)
        {
            return Array.Empty<EntitlementDataScope>();
        }

        // Load the tenant org-unit tree once (non-deleted); drop archived units.
        var orgUnitList = (await organizationUnits.GetAllAsync(cancellationToken))
            .Where(o => o.TenantId == tenantId && !o.IsArchived)
            .ToList();
        var orgUnitById = orgUnitList.ToDictionary(o => o.Id);
        var childrenByParent = BuildChildrenMap(orgUnitList);

        var scopes = new List<EntitlementDataScope>();

        AddOrgUnitScopes(activePositions, orgUnitById, childrenByParent, scopes);
        AddPositionScopes(activePositions, scopes);
        await AddManagerChainScopesAsync(activePositions, cancellationToken, scopes);
        await AddLegalEntityScopesAsync(activePositions, orgUnitById, cancellationToken, scopes);

        return scopes;
    }

    private static Dictionary<Guid, List<Guid>> BuildChildrenMap(IReadOnlyList<OrganizationUnit> orgUnits)
    {
        var map = new Dictionary<Guid, List<Guid>>();
        foreach (var unit in orgUnits)
        {
            if (unit.ParentOrganizationUnitId is not { } parentId)
            {
                continue;
            }

            if (!map.TryGetValue(parentId, out var children))
            {
                children = new List<Guid>();
                map[parentId] = children;
            }

            children.Add(unit.Id);
        }

        return map;
    }

    // OrgUnit scope = own + subtree: the user's own Organization Unit(s) plus all descendants, pre-expanded
    // into a flat OrgUnitIds list. No subtree marker, no new enum value (OD-FU15-1, locked).
    private static void AddOrgUnitScopes(
        IReadOnlyList<Position> activePositions,
        IReadOnlyDictionary<Guid, OrganizationUnit> orgUnitById,
        IReadOnlyDictionary<Guid, List<Guid>> childrenByParent,
        List<EntitlementDataScope> scopes)
    {
        var collected = new HashSet<Guid>();
        var queue = new Queue<Guid>();

        foreach (var position in activePositions)
        {
            if (orgUnitById.ContainsKey(position.OrganizationUnitId)
                && collected.Add(position.OrganizationUnitId))
            {
                queue.Enqueue(position.OrganizationUnitId);
            }
        }

        // Breadth-first descendant expansion; the `collected` set doubles as a cycle guard.
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (orgUnitById.ContainsKey(childId) && collected.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        foreach (var orgUnitId in collected)
        {
            var code = orgUnitById.TryGetValue(orgUnitId, out var unit) ? unit.Code : null;
            scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, orgUnitId, code));
        }
    }

    private static void AddPositionScopes(IReadOnlyList<Position> activePositions, List<EntitlementDataScope> scopes)
    {
        foreach (var position in activePositions)
        {
            scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.Position, position.Id, position.Code));
        }
    }

    // ManagerChain scope = the Position IDs in the user's Position reporting chain (derived from
    // Position.ReportsToPositionId) — NOT Org Unit IDs. Cycle-safe, max depth 32 (mirrors GetManagerChainQuery).
    private async Task AddManagerChainScopesAsync(
        IReadOnlyList<Position> activePositions,
        CancellationToken cancellationToken,
        List<EntitlementDataScope> scopes)
    {
        // Seed visited with the user's own active positions so they are never emitted as managers.
        var visited = new HashSet<Guid>(activePositions.Select(p => p.Id));
        var emitted = new HashSet<Guid>();

        foreach (var position in activePositions)
        {
            var cursor = position.ReportsToPositionId;

            for (var depth = 1; cursor.HasValue && depth <= MaxManagerChainDepth; depth++)
            {
                // Cycle / already-walked guard: stop this branch on revisit.
                if (!visited.Add(cursor.Value))
                {
                    break;
                }

                var manager = await positions.GetByIdAsync(cursor.Value, cancellationToken);
                if (manager is null || manager.IsArchived)
                {
                    break;
                }

                if (emitted.Add(manager.Id))
                {
                    scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.ManagerChain, manager.Id, manager.Code));
                }

                cursor = manager.ReportsToPositionId;
            }
        }
    }

    // LegalEntity scope = the read-only MOD-0220 reference owned by the user's own Organization Unit(s).
    //
    // Fail-closed: the stale `LegalEntityId` stored on the Organization Unit is NOT sufficient on its own. Each
    // candidate id is re-confirmed against the verified MOD-0220 read-only lookup-validation contract
    // (ILegalEntityReferenceValidator → MdmLegalEntityReferenceValidator, AG-STEP-003). The scope is emitted ONLY
    // when the live result is currently referenceable. The validator is itself fail-closed (non-2xx, archived/
    // deleted via lifecycle, cross-tenant, Referenceable != true, id mismatch, timeout/network/JSON all collapse to
    // an unsuccessful response); this method additionally swallows any unexpected validator exception so a Legal
    // Entity lookup failure never produces a scope and never discards the already-computed OrgUnit/Position/
    // ManagerChain scopes. Cancellation still propagates.
    private async Task AddLegalEntityScopesAsync(
        IReadOnlyList<Position> activePositions,
        IReadOnlyDictionary<Guid, OrganizationUnit> orgUnitById,
        CancellationToken cancellationToken,
        List<EntitlementDataScope> scopes)
    {
        var seen = new HashSet<Guid>();

        foreach (var position in activePositions)
        {
            if (!orgUnitById.TryGetValue(position.OrganizationUnitId, out var unit)
                || unit.LegalEntityId == Guid.Empty
                || !seen.Add(unit.LegalEntityId))
            {
                continue;
            }

            Response<LegalEntityReferenceDto> validation;
            try
            {
                validation = await legalEntityValidator.ValidateAsync(unit.LegalEntityId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Fail-closed: validator failure → no LegalEntity scope for this id; other scopes are unaffected.
                continue;
            }

            if (validation.IsSuccessful && validation.Data?.Referenceable == true)
            {
                scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, unit.LegalEntityId, null));
            }
        }
    }
}
