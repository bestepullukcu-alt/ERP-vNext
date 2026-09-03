using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// (c) WHO gets which report — the authority model, at the request.
///
/// <para><b>Two mechanisms, deliberately not one.</b> The PERMISSION
/// (<c>TaskPermissions.WorkReportRead</c>) decides whether a caller may open a report; the SCOPE decides whose
/// rows it contains, and it comes from MOD-0018-FU15's <c>IDataScopeResolver</c>. Widening to the whole tenant
/// is a SECOND permission and never a field on the request — a flag would let anyone who can reach the endpoint
/// set it.</para>
///
/// <para>Oracle frames worklist reports the same way: the scope is the user's groups or their reportees'
/// groups, not the company.</para>
/// </summary>
public sealed class WorkReportQueryHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Caller = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid MyUnit = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Records the criteria it was handed — the only way to see what the handler actually asked for.</summary>
    private sealed class RecordingReports : IWorkReportRepository
    {
        public WorkReportCriteria? LastCriteria { get; private set; }
        public int Calls { get; private set; }

        public System.Threading.Tasks.Task<WorkReportDto> AggregateAsync(
            WorkReportCriteria criteria, CancellationToken ct = default)
        {
            Calls++;
            LastCriteria = criteria;

            /*
             * ⚠ NOT `WorkReportDto.Empty` — that helper hardcodes `scoped`, correctly, because it is the
             * FAIL-CLOSED answer and a tenant-wide scope can never match nothing. Using it here made the
             * tenant-wide test fail against a stub rather than against the handler. The real repository reports
             * the scope it was given, so the stub does too.
             */
            return System.Threading.Tasks.Task.FromResult(new WorkReportDto(
                criteria.From,
                criteria.To,
                criteria.Scope.TenantWide ? WorkReportDto.ScopeTenant : WorkReportDto.ScopeScoped,
                criteria.GroupBy,
                WorkReportDto.EmptyBucket(null),
                []));
        }
    }

    private sealed class StubScopes(IReadOnlyList<EntitlementDataScope>? scopes, Exception? throws = null)
        : IDataScopeResolver
    {
        public int Calls { get; private set; }

        public System.Threading.Tasks.Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
            Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken cancellationToken)
        {
            Calls++;
            if (throws is not null)
            {
                throw throws;
            }

            return System.Threading.Tasks.Task.FromResult(scopes ?? []);
        }
    }

    private static (WorkReportQueryHandler Handler, RecordingReports Reports, StubScopes Scopes) Build(
        IReadOnlyList<EntitlementDataScope>? scopes,
        FakeActorPermissions permissions,
        Exception? resolverThrows = null,
        Guid? callerOverride = null)
    {
        var reports = new RecordingReports();
        var resolver = new StubScopes(scopes, resolverThrows);
        var handler = new WorkReportQueryHandler(
            reports,
            resolver,
            new FakeCurrentUserContext(callerOverride ?? Caller),
            new FakeTenantContext(Tenant),
            permissions,
            NullLogger<WorkReportQueryHandler>.Instance);

        return (handler, reports, resolver);
    }

    private static WorkReportQuery Query(WorkReportGroupBy groupBy = WorkReportGroupBy.None) =>
        new(From, To, groupBy, "corr");

    private static EntitlementDataScope Unit(Guid id) =>
        new(EntitlementDataScopeKind.OrgUnit, id, scopeCode: null);

    // ── (c) THE DEFAULT IS THE CALLER'S OWN SCOPE ────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_the_tenant_wide_permission_the_report_is_narrowed_to_what_the_caller_may_see()
    {
        /*
         * ⚠ NARROWED, NOT REFUSED. A person holding WorkReportRead is entitled to a report — of their own work.
         * Answering 403 would make the ordinary case look like a misconfiguration, and the screen would grow a
         * second rendering path for a state that is not an error.
         */
        var (handler, reports, resolver) = Build(
            [Unit(MyUnit)], TaskActors.Holding(TaskPermissions.WorkReportRead));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, reports.Calls);

        var scope = reports.LastCriteria!.Scope;
        Assert.False(scope.TenantWide);
        Assert.Equal([MyUnit], scope.OrganizationUnitIds);
        Assert.Equal(WorkReportDto.ScopeScoped, response.Data!.ScopeApplied);
    }

    [Fact]
    public async Task The_tenant_wide_permission_widens_it_and_the_resolver_is_not_even_asked()
    {
        /*
         * The second key does what its name says. The resolver is skipped entirely — walking an org tree to
         * produce a filter that will be discarded is work for nothing, and its absence is the clearest possible
         * statement that the permission, not the org chart, decided this.
         */
        var (handler, reports, resolver) = Build(
            [Unit(MyUnit)],
            TaskActors.Holding(TaskPermissions.WorkReportRead, TaskPermissions.WorkReportReadTenantWide));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(reports.LastCriteria!.Scope.TenantWide);
        Assert.Equal(0, resolver.Calls);
        Assert.Equal(WorkReportDto.ScopeTenant, response.Data!.ScopeApplied);
    }

    [Fact]
    public async Task Holding_only_the_narrow_key_can_never_produce_a_tenant_wide_scope()
    {
        /*
         * ⚠ THE ESCALATION GUARD. Even with an unusually generous data scope — every kind the resolver can
         * emit, all at once — the answer stays `scoped`. Tenant-wide is reachable only through the permission,
         * so a scope that happens to cover a lot cannot quietly become a scope that covers everything.
         */
        var everything = new[]
        {
            Unit(MyUnit),
            new EntitlementDataScope(EntitlementDataScopeKind.Position, Guid.NewGuid(), null),
            new EntitlementDataScope(EntitlementDataScopeKind.ManagerChain, Guid.NewGuid(), null),
            new EntitlementDataScope(EntitlementDataScopeKind.Own, null, null)
        };

        var (handler, reports, _) = Build(everything, TaskActors.Holding(TaskPermissions.WorkReportRead));

        await handler.Handle(Query(), CancellationToken.None);

        Assert.False(reports.LastCriteria!.Scope.TenantWide);
    }

    [Fact]
    public async Task A_platform_actor_gets_the_tenant_wide_report()
    {
        // A platform actor is above a single tenant and passes every permission by definition. Making it walk
        // the org tree of a tenant it does not belong to would resolve to nothing and hand an empty page to the
        // one caller entitled to everything.
        var (handler, reports, resolver) = Build(null, TaskActors.PermitAll());

        await handler.Handle(Query(), CancellationToken.None);

        Assert.True(reports.LastCriteria!.Scope.TenantWide);
        Assert.Equal(0, resolver.Calls);
    }

    // ── (b) FAIL-CLOSED AT THE REQUEST ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unresolvable_scope_returns_an_EMPTY_report_and_never_reaches_the_database()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED.
         *
         * `OrgDataScopeResolver` fails closed three separate ways — no user id, no active position assignment,
         * no live position — and each returns an empty list. Reading that as "no restrictions" is what turns a
         * permission model into decoration, and a report is where it would go unnoticed longest: the page
         * renders, the numbers look plausible, and they are somebody else's.
         *
         * `Calls == 0` is the load-bearing half. A repository that was never asked cannot have answered with
         * the wrong rows.
         */
        var (handler, reports, _) = Build([], TaskActors.Holding(TaskPermissions.WorkReportRead));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, reports.Calls);
        Assert.Equal(0, response.Data!.Totals.Flow.Opened);
        Assert.Empty(response.Data.Groups);
        Assert.Equal(WorkReportDto.ScopeScoped, response.Data.ScopeApplied);
    }

    [Fact]
    public async Task A_THROWING_resolver_reports_nothing_rather_than_everything()
    {
        /*
         * The failure this catches is not a crash — it is the temptation to "handle" one. Letting it propagate
         * is a 500; swallowing it and carrying on with no scope is catastrophic, because "no scope" is one
         * careless line from "no filter". Empty, and loudly logged.
         */
        var (handler, reports, _) = Build(
            null, TaskActors.Holding(TaskPermissions.WorkReportRead), new InvalidOperationException("org tree down"));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, reports.Calls);
    }

    [Fact]
    public async Task An_unidentifiable_caller_gets_nothing()
    {
        // There is nobody to compute a scope for. The resolver would fail closed on this too; refusing here
        // means it is not asked at all.
        var (handler, reports, resolver) = Build(
            [Unit(MyUnit)], TaskActors.Holding(TaskPermissions.WorkReportRead), callerOverride: Guid.Empty);

        await handler.Handle(Query(), CancellationToken.None);

        Assert.Equal(0, reports.Calls);
        Assert.Equal(0, resolver.Calls);
    }

    // ── The period is required and bounded ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]   // same instant
    [InlineData(-1)]  // inverted
    public async Task A_period_that_does_not_move_forward_is_refused_rather_than_answered_emptily(int dayOffset)
    {
        /*
         * An unbounded report is a full-collection scan wearing a date picker — MEASURED 2026-09-03, the only
         * broad read this module had was `GetAllForTenantAsync`, which loads every task in the tenant. And an
         * INVERTED period is a caller who transposed two dates: answering "nothing happened" would be a
         * confident, wrong answer where a correction is cheap.
         */
        var (handler, reports, _) = Build([Unit(MyUnit)], TaskActors.Holding(TaskPermissions.WorkReportRead));

        var response = await handler.Handle(
            new WorkReportQuery(From, From.AddDays(dayOffset), WorkReportGroupBy.None, "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ValidationFailed, response.ReasonCode);
        Assert.Equal(0, reports.Calls);
    }

    [Fact]
    public async Task The_requested_period_and_grouping_reach_the_query_unchanged()
    {
        // The handler narrows WHOSE rows are counted and nothing else. A period or an axis quietly altered on
        // the way through would make the screen and the numbers disagree about what was asked.
        var (handler, reports, _) = Build(
            [Unit(MyUnit)], TaskActors.Holding(TaskPermissions.WorkReportRead));

        await handler.Handle(Query(WorkReportGroupBy.TaskType), CancellationToken.None);

        Assert.Equal(From, reports.LastCriteria!.From);
        Assert.Equal(To, reports.LastCriteria.To);
        Assert.Equal(WorkReportGroupBy.TaskType, reports.LastCriteria.GroupBy);
    }
}
