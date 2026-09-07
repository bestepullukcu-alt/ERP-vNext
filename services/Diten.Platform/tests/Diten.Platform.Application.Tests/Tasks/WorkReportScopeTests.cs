using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WHOSE work the report counts.
///
/// <para><b>The question changed, and that is why this exists.</b> Everything MOD-0024 had built until now asked
/// "what is MY work" (<c>GetMyWorkItemsQuery</c>). A report asks "whose work may I SEE" — a different question
/// with a different answer, and one this product already had an engine for: MOD-0018-FU15's
/// <c>IDataScopeResolver</c>, registered as <c>OrgDataScopeResolver</c>
/// (<c>DependencyInjection.cs:59</c>, measured 2026-09-03). Nothing here computes a scope; it TRANSLATES one.</para>
///
/// <para><b>⚠ THE FAILURE MODE THESE GUARD IS NOT A CRASH.</b> An unscoped report renders perfectly, sums
/// correctly, and is simply about somebody else's work. Nobody files a bug for it. So every path that cannot
/// establish a scope has to end in NOTHING, and each of those paths is pinned below.</para>
/// </summary>
public sealed class WorkReportScopeTests
{
    private static readonly Guid Caller = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MyUnit = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ChildUnit = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ForeignUnit = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MyPosition = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ForeignPosition = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Stranger = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static EntitlementDataScope Scope(EntitlementDataScopeKind kind, Guid? id = null, bool include = true)
        => new(kind, id, scopeCode: null, isInclude: include);

    private static WorkReportRow Row(
        Guid? unit = null,
        Guid? pool = null,
        Guid? assignee = null,
        Guid? requester = null) => new(
        Guid.NewGuid(),
        TaskTypeId: null,
        OrganizationUnitId: unit ?? ForeignUnit,
        AssigneeUserId: assignee,
        CreatedByUserId: requester,
        PoolPositionId: pool,
        Priority: TaskPriority.Medium,
        CreatedAt: DateTimeOffset.UtcNow,
        CompletedAt: null,
        CancelledAt: null,
        DueAt: null,
        EstimateHours: null,
        SpentHours: 0m,
        ClosureReasonCode: null,
        Lifecycle: TaskLifecycle.Open);

    // ── (b) FAIL-CLOSED ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void No_scopes_at_all_means_NOTHING_rather_than_everything()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED.
         *
         * `OrgDataScopeResolver` fails closed in three separate places — no user id, no active position
         * assignment, no live position — and each returns an EMPTY list. The tempting reading of "no scopes" is
         * "no restrictions", and that reading is what turns an authorization model into decoration.
         */
        Assert.True(WorkReportScope.FromDataScopes([], Caller).MatchesNothing);
        Assert.True(WorkReportScope.FromDataScopes(null, Caller).MatchesNothing);
        Assert.True(WorkReportScope.Empty.MatchesNothing);
    }

    [Fact]
    public void An_empty_scope_matches_no_row_even_when_rows_exist()
    {
        /*
         * ⚠ NON-VACUITY, and the reason this test is separate from the one above.
         *
         * `MatchesNothing == true` is a property of an object; it proves nothing about what the report counts.
         * This asks the same predicate the query asks, against REAL rows that a wider scope would match — so a
         * regression that made an empty scope behave like "no filter" fails here rather than passing quietly.
         */
        var rows = new[]
        {
            Row(unit: MyUnit),
            Row(pool: MyPosition),
            Row(assignee: Caller),
            Row(requester: Caller)
        };

        Assert.All(rows, row => Assert.False(WorkReportScopeMirror.InScope(WorkReportScope.Empty, row)));

        // And the same rows ARE matched by a scope that includes them — otherwise the assertion above would
        // hold for a predicate that simply always returns false.
        var wide = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.OrgUnit, MyUnit),
             Scope(EntitlementDataScopeKind.Position, MyPosition),
             Scope(EntitlementDataScopeKind.Own)],
            Caller);

        Assert.All(rows, row => Assert.True(WorkReportScopeMirror.InScope(wide, row)));
    }

    [Fact]
    public void A_scope_kind_the_task_cannot_carry_narrows_rather_than_widens()
    {
        /*
         * MEASURED 2026-09-03: `TaskItem` carries `OrganizationUnitId`, `PoolPositionId`, `AssigneeUserId` and
         * `CreatedByUserId` — and NO legal-entity, company, country or region field. The resolver emits
         * LegalEntity scopes anyway (its `AddLegalEntityScopesAsync`), so they arrive here with nowhere to land.
         *
         * Dropping them NARROWS the answer, which is the direction that cannot leak. Guessing a unit→entity
         * join here to "use" them would be the second engine the assignment resolver's own comment warns about.
         */
        var scope = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.LegalEntity, Guid.NewGuid())], Caller);

        Assert.True(scope.MatchesNothing);
    }

    [Fact]
    public void An_EXCLUDE_scope_collapses_the_answer_instead_of_being_ignored()
    {
        /*
         * `EntitlementDataScope.IsInclude` can be false. Subtracting an exclusion properly needs a rule this
         * slice does not have — and a report that silently DROPPED the exclusion would show rows somebody was
         * deliberately denied. Seeing nothing is the wrong answer in the safe direction; seeing too much is the
         * wrong answer in the direction that matters.
         */
        var scope = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.OrgUnit, MyUnit),
             Scope(EntitlementDataScopeKind.OrgUnit, ChildUnit, include: false)],
            Caller);

        Assert.True(scope.MatchesNothing);
        Assert.False(WorkReportScopeMirror.InScope(scope, Row(unit: MyUnit)));
    }

    // ── (a) OUT-OF-SCOPE WORK IS NOT COUNTED ─────────────────────────────────────────────────────────────

    [Fact]
    public void Work_outside_my_organisation_is_excluded_while_work_inside_it_is_counted()
    {
        /*
         * ⚠ BOTH HALVES IN ONE TEST, ON PURPOSE. "The foreign row is absent" is worthless on its own — it is
         * equally true of a report that counted nothing at all. The mine/theirs pair is what makes the
         * exclusion a measurement.
         *
         * The unit list arrives ALREADY subtree-expanded: the resolver's own comment says OrgUnit scope is
         * "own + subtree … pre-expanded into a flat OrgUnitIds list", so the child unit is a separate entry
         * rather than something this class walks to.
         */
        var scope = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.OrgUnit, MyUnit),
             Scope(EntitlementDataScopeKind.OrgUnit, ChildUnit)],
            Caller);

        Assert.True(WorkReportScopeMirror.InScope(scope, Row(unit: MyUnit)));
        Assert.True(WorkReportScopeMirror.InScope(scope, Row(unit: ChildUnit)));
        Assert.False(WorkReportScopeMirror.InScope(scope, Row(unit: ForeignUnit)));
    }

    [Fact]
    public void Pooled_work_counts_for_my_positions_and_for_my_managers_but_not_for_a_stranger_position()
    {
        /*
         * ManagerChain points UP — the positions ABOVE the caller. The assignment resolver's own comment says
         * why: "data scoping asks 'whose rows may I see through my superiors'". A report asks exactly that, so
         * it uses the resolver's direction as it comes rather than the downward walk assignment had to derive.
         */
        var scope = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.Position, MyPosition),
             Scope(EntitlementDataScopeKind.ManagerChain, Guid.Parse("88888888-8888-8888-8888-888888888888"))],
            Caller);

        Assert.True(WorkReportScopeMirror.InScope(scope, Row(pool: MyPosition)));
        Assert.True(WorkReportScopeMirror.InScope(
            scope, Row(pool: Guid.Parse("88888888-8888-8888-8888-888888888888"))));
        Assert.False(WorkReportScopeMirror.InScope(scope, Row(pool: ForeignPosition)));
    }

    [Fact]
    public void An_Own_scope_covers_work_I_hold_and_work_I_raised_and_nobody_elses()
    {
        // `Own` carries no id — it MEANS the caller — so the caller's own id is what lands in the scope.
        var scope = WorkReportScope.FromDataScopes([Scope(EntitlementDataScopeKind.Own)], Caller);

        Assert.True(WorkReportScopeMirror.InScope(scope, Row(assignee: Caller)));
        Assert.True(WorkReportScopeMirror.InScope(scope, Row(requester: Caller)));
        Assert.False(WorkReportScopeMirror.InScope(scope, Row(assignee: Stranger, requester: Stranger)));
    }

    [Fact]
    public void Tenant_wide_sees_every_row_and_is_reachable_only_through_its_own_factory()
    {
        /*
         * A separate factory rather than a boolean argument: a call site cannot widen the scope by passing
         * `true` from a variable that once meant something else. The permission check that guards it lives in
         * the handler and is pinned by WorkReportQueryHandlerTests.
         */
        var scope = WorkReportScope.TenantWideScope();

        Assert.True(scope.TenantWide);
        Assert.False(scope.MatchesNothing);
        Assert.True(WorkReportScopeMirror.InScope(scope, Row(unit: ForeignUnit, assignee: Stranger)));

        // And no combination of ordinary data scopes can produce it.
        var ordinary = WorkReportScope.FromDataScopes(
            [Scope(EntitlementDataScopeKind.OrgUnit, MyUnit), Scope(EntitlementDataScopeKind.Own)], Caller);
        Assert.False(ordinary.TenantWide);
    }
}
