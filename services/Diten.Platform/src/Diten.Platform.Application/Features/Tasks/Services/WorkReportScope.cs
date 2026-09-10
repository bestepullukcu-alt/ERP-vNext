using Diten.Platform.Common.Authorization;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WHOSE work the report counts — the data scope, translated into the handful of fields a task actually carries.
///
/// <para><b>It computes no scopes, and that is the whole design.</b> MOD-0018-FU15's
/// <see cref="IDataScopeResolver"/> already turns the org master data into OrgUnit / Position / ManagerChain /
/// LegalEntity scopes, cycle-safe and fail-closed, and it is registered
/// (<c>DependencyInjection.cs:59</c> → <c>OrgDataScopeResolver</c>). A second engine beside it would be the K6
/// defect this module's own <c>TaskAssignmentScopeResolver</c> names: two places to disagree about the same
/// truth. This TRANSLATES those scopes into the one question the report asks.</para>
///
/// <para><b>And it is the DATA scope, not the assignment scope.</b> The two run in opposite directions, and the
/// assignment resolver's own comment says so: <c>ManagerChain</c> holds the positions ABOVE me, "because data
/// scoping asks 'whose rows may I see through my superiors'". A report asks exactly that question, so it uses
/// the resolver directly rather than the downward walk assignment needed.</para>
///
/// <para><b>Fail-closed is a shape, not a check.</b> An empty scope list produces
/// <see cref="Empty"/>, whose <see cref="MatchesNothing"/> is true — the query then returns no rows rather than
/// every row. The dangerous default is not "refuse"; it is "no filter", which in a report reads as success.</para>
/// </summary>
public sealed class WorkReportScope
{
    private WorkReportScope(
        bool tenantWide,
        IReadOnlySet<Guid> organizationUnitIds,
        IReadOnlySet<Guid> positionIds,
        IReadOnlySet<Guid> userIds)
    {
        TenantWide = tenantWide;
        OrganizationUnitIds = organizationUnitIds;
        PositionIds = positionIds;
        UserIds = userIds;
    }

    /// <summary>Every row in the tenant. Only ever produced by <see cref="TenantWideScope"/>.</summary>
    public bool TenantWide { get; }

    /// <summary>Units the caller may see, ALREADY subtree-expanded by the resolver (its own comment: "own +
    /// subtree … pre-expanded into a flat OrgUnitIds list").</summary>
    public IReadOnlySet<Guid> OrganizationUnitIds { get; }

    /// <summary>Positions whose pooled work counts — the caller's own, plus their manager chain.</summary>
    public IReadOnlySet<Guid> PositionIds { get; }

    /// <summary>Individuals whose work counts, for resolvers that emit <c>Own</c>/<c>Assigned</c> scopes.</summary>
    public IReadOnlySet<Guid> UserIds { get; }

    /// <summary>
    /// Nothing is in scope. The query must return an EMPTY report — never an unfiltered one.
    /// </summary>
    public bool MatchesNothing =>
        !TenantWide
        && OrganizationUnitIds.Count == 0
        && PositionIds.Count == 0
        && UserIds.Count == 0;

    /// <summary>The fail-closed answer: no scopes resolved, so no rows.</summary>
    public static WorkReportScope Empty { get; } = new(
        false,
        new HashSet<Guid>(),
        new HashSet<Guid>(),
        new HashSet<Guid>());

    /// <summary>
    /// The whole tenant — built ONLY where <c>TaskPermissions.WorkReportReadTenantWide</c> has been checked.
    /// A separate factory rather than a boolean argument, so a call site cannot widen the scope by passing
    /// <c>true</c> from a variable that once meant something else.
    /// </summary>
    public static WorkReportScope TenantWideScope() => new(
        true,
        new HashSet<Guid>(),
        new HashSet<Guid>(),
        new HashSet<Guid>());

    /// <summary>
    /// The caller's own scope, from what <see cref="IDataScopeResolver"/> resolved.
    ///
    /// <para><b>EXCLUDE scopes collapse the whole answer to nothing.</b> <c>EntitlementDataScope.IsInclude</c>
    /// can be false, and a report that silently ignored an exclusion would show rows somebody was deliberately
    /// denied. Subtracting it properly needs a rule this slice does not have — so the honest answer is to see
    /// nothing rather than to see too much, and the exclusion is recorded as unsupported rather than dropped.</para>
    ///
    /// <para><b>LegalEntity / Company / Country / Region are NOT translated, and that is safe.</b> MEASURED
    /// 2026-09-03: <c>TaskItem</c> carries no legal-entity, company, country or region field — only
    /// <c>OrganizationUnitId</c>, <c>PoolPositionId</c>, <c>AssigneeUserId</c> and <c>CreatedByUserId</c>. A
    /// scope with nowhere to land NARROWS the answer, which is the direction that cannot leak. Widening it by
    /// guessing a unit→entity join here would be the second engine again.</para>
    /// </summary>
    public static WorkReportScope FromDataScopes(IReadOnlyList<EntitlementDataScope>? scopes, Guid callerUserId)
    {
        if (scopes is not { Count: > 0 })
        {
            return Empty;
        }

        if (scopes.Any(scope => !scope.IsInclude))
        {
            return Empty;
        }

        var units = new HashSet<Guid>();
        var positions = new HashSet<Guid>();
        var users = new HashSet<Guid>();

        foreach (var scope in scopes)
        {
            switch (scope.Kind)
            {
                // The resolver pre-expands the subtree, so these are already every unit the caller may see.
                case EntitlementDataScopeKind.OrgUnit:
                case EntitlementDataScopeKind.Department:
                case EntitlementDataScopeKind.Team:
                    Add(units, scope);
                    break;

                // A task pooled to one of these positions is work the caller may see. ManagerChain points UP —
                // the positions above the caller — which is the direction data scoping is built in.
                case EntitlementDataScopeKind.Position:
                case EntitlementDataScopeKind.ManagerChain:
                    Add(positions, scope);
                    break;

                /*
                 * The caller as an individual. `Own` and `RecordOwner` carry no id — they MEAN the caller — so
                 * the caller's own id is what lands here. `Assigned` may name someone; when it does not, it also
                 * means the caller.
                 */
                case EntitlementDataScopeKind.Own:
                case EntitlementDataScopeKind.RecordOwner:
                case EntitlementDataScopeKind.Assigned:
                    if (scope.ScopeId is { } named && named != Guid.Empty)
                    {
                        users.Add(named);
                    }
                    else if (callerUserId != Guid.Empty)
                    {
                        users.Add(callerUserId);
                    }

                    break;

                // See the note above: a scope with no field to land on narrows, and narrowing is safe.
                default:
                    break;
            }
        }

        return units.Count == 0 && positions.Count == 0 && users.Count == 0
            ? Empty
            : new WorkReportScope(false, units, positions, users);
    }

    private static void Add(HashSet<Guid> target, EntitlementDataScope scope)
    {
        if (scope.ScopeId is { } id && id != Guid.Empty)
        {
            target.Add(id);
        }
    }
}
