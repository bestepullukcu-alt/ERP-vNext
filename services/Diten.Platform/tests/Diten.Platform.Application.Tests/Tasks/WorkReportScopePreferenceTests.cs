using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// DILIM 1f — "TÜM KİRACI" ↔ "SİZİN KAPSAMINIZ", a DISPLAY preference that can only narrow.
///
/// <para><b>⚠ THE WHOLE SLICE IS ONE RULE.</b> <see cref="WorkReportScopePreference.Own"/> is honoured even
/// from a caller entitled to the whole tenant — narrowing what you already hold is never a security question.
/// <see cref="WorkReportScopePreference.Tenant"/> is honoured ONLY when the caller's permission already allows
/// it; asked for by anyone else it is silently ignored, never rejected — the same fail-closed-but-quiet shape
/// Faz 5a already established for a scope that resolves to nothing.</para>
///
/// <para><b>These tests exercise the REAL seam, not a copy of it.</b> `WorkReportScopeSource` is constructed
/// exactly as production wires it (`WorkReportQueryHandlerTests.Scope`'s own helper, reused here rather than
/// re-implemented) — the gap that let a CONTROL TOWER sabotage stay green in Dilim 1a, and again in 1c, was
/// always a test measuring its own reconstruction of a rule instead of the shipped path.</para>
/// </summary>
public sealed class WorkReportScopePreferenceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Caller = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid MyUnit = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class StubScopes(IReadOnlyList<EntitlementDataScope>? scopes) : IDataScopeResolver
    {
        public Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
            Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken cancellationToken)
            => Task.FromResult(scopes ?? []);
    }

    private static EntitlementDataScope Unit(Guid id) => new(EntitlementDataScopeKind.OrgUnit, id, scopeCode: null);

    /// <summary>THE SHIPPED SOURCE, wired exactly as `WorkReportQueryHandlerTests.Scope` wires it — not a copy.</summary>
    private static WorkReportScopeSource Source(FakeActorPermissions permissions, IReadOnlyList<EntitlementDataScope>? myScopes = null) =>
        new(
            new StubScopes(myScopes ?? [Unit(MyUnit)]),
            new FakeCurrentUserContext(Caller),
            new FakeTenantContext(Tenant),
            permissions,
            NullLogger<WorkReportScopeSource>.Instance);

    // ── (1) THE CORE RULE: narrow-only, both directions ─────────────────────────────────────────────────────

    [Fact]
    public async Task An_UNPRIVILEGED_caller_asking_for_Tenant_is_silently_given_their_OWN_scope()
    {
        /*
         * ⚠ THE MOST CRITICAL GUARD IN THIS SLICE — a privilege-escalation attempt that must not work, and must
         * fail QUIETLY: a 403 here would be honest but would hand an attacker a confirmation that the parameter
         * exists and is being checked. The caller simply gets what they were always going to get.
         */
        var source = Source(TaskActors.None());

        var scope = await source.ResolveAsync(WorkReportScopePreference.Tenant);

        Assert.False(scope.TenantWide, "an unprivileged 'tenant' request was honoured — this is privilege escalation");
        Assert.Contains(MyUnit, scope.OrganizationUnitIds);
    }

    [Fact]
    public async Task A_PRIVILEGED_caller_asking_for_Own_is_narrowed_even_though_they_could_see_everything()
    {
        var source = Source(TaskActors.Holding(TaskPermissions.WorkReportReadTenantWide));

        var scope = await source.ResolveAsync(WorkReportScopePreference.Own);

        Assert.False(scope.TenantWide, "'own' was ignored and the tenant-wide grant leaked through anyway");
        Assert.Contains(MyUnit, scope.OrganizationUnitIds);
    }

    [Fact]
    public async Task A_PRIVILEGED_caller_asking_for_Tenant_still_gets_the_whole_tenant()
    {
        // Non-vacuity for the two tests above: `Tenant` is not simply ignored outright — it works for the
        // caller it was always going to work for.
        var source = Source(TaskActors.Holding(TaskPermissions.WorkReportReadTenantWide));

        var scope = await source.ResolveAsync(WorkReportScopePreference.Tenant);

        Assert.True(scope.TenantWide);
    }

    [Fact]
    public async Task A_PLATFORM_ACTOR_asking_for_Own_is_narrowed_to_their_resolved_scope_too()
    {
        // `IsPlatformActor` is the OTHER path to tenant-wide (WorkReportScopeSource's own permission check ORs
        // the two) — `Own` has to short-circuit ahead of THAT branch as well, not just the permission-key one.
        var source = Source(new FakeActorPermissions(isPlatformActor: true));

        var scope = await source.ResolveAsync(WorkReportScopePreference.Own);

        Assert.False(scope.TenantWide, "a platform actor's 'own' request still returned tenant-wide");
    }

    // ── (2) NO PARAMETER = NO CHANGE — the backward-compatibility guard ─────────────────────────────────────

    [Fact]
    public async Task No_preference_at_all_behaves_EXACTLY_as_it_did_before_this_slice()
    {
        /*
         * ⚠ THE REGRESSION GUARD. Every caller that existed before Dilim 1f never sends this parameter at all;
         * `null` is not "ask for own" and not "ask for tenant" — it is "decide the way you always decided",
         * which is the permission check alone.
         */
        var privileged = await Source(TaskActors.Holding(TaskPermissions.WorkReportReadTenantWide))
            .ResolveAsync(preference: null);
        Assert.True(privileged.TenantWide);

        var unprivileged = await Source(TaskActors.None()).ResolveAsync(preference: null);
        Assert.False(unprivileged.TenantWide);
        Assert.Contains(MyUnit, unprivileged.OrganizationUnitIds);
    }

    // ── (3) ScopeApplied REPORTS WHAT WAS APPLIED, NEVER WHAT WAS ASKED ─────────────────────────────────────

    [Fact]
    public async Task Handle_reports_the_APPLIED_scope_in_ScopeApplied_never_the_REQUESTED_one()
    {
        /*
         * ⚠ THIS IS THE SCREEN'S OWN "no work" vs "no work I may see" DISTINCTION, one level up: a reader who
         * asked for tenant-wide and quietly got their own scope back must be told THAT, or they draw a
         * conclusion about the whole tenant from a number that was only ever about their corner of it.
         */
        var reports = new RecordingReports();
        var handler = new WorkReportQueryHandler(
            reports, Source(TaskActors.None()));

        var response = await handler.Handle(
            new WorkReportQuery(From, To, WorkReportGroupBy.None, "corr", ScopePreference: WorkReportScopePreference.Tenant),
            CancellationToken.None);

        Assert.Equal(WorkReportDto.ScopeScoped, response.Data!.ScopeApplied);
    }

    [Fact]
    public async Task Handle_reports_tenant_when_a_privileged_caller_actually_receives_it()
    {
        // Non-vacuity: the field is not hard-coded to "scoped" — it reflects reality in both directions.
        var reports = new RecordingReports();
        var handler = new WorkReportQueryHandler(
            reports, Source(TaskActors.Holding(TaskPermissions.WorkReportReadTenantWide)));

        var response = await handler.Handle(
            new WorkReportQuery(From, To, WorkReportGroupBy.None, "corr", ScopePreference: WorkReportScopePreference.Tenant),
            CancellationToken.None);

        Assert.Equal(WorkReportDto.ScopeTenant, response.Data!.ScopeApplied);
    }

    // ── (4) THE ITEMS ENDPOINT CARRIES THE SAME PREFERENCE — the 1c identity, one axis further ─────────────

    [Fact]
    public async Task The_items_endpoint_honours_the_SAME_narrow_only_rule_as_the_report()
    {
        /*
         * ⚠ A SECOND DOOR INTO THE SAME ROOM. Dilim 1c's whole point was that a click cannot open more than the
         * tile it came from reported; if the items handler resolved `Tenant` independently for an unprivileged
         * caller, the DRILL-DOWN would leak the whole tenant even though the report tile it was opened from
         * only ever showed the caller's own scope.
         */
        var reports = new RecordingReports();
        var handler = new WorkReportItemsQueryHandler(
            reports, Source(TaskActors.None()));

        await handler.Handle(
            new WorkReportItemsQuery(
                From, To, WorkReportBucketKind.Opened, "corr", ScopePreference: WorkReportScopePreference.Tenant),
            CancellationToken.None);

        Assert.False(reports.LastCriteria!.Scope.TenantWide, "the items endpoint honoured an unprivileged tenant-wide request");
    }

    /// <summary>Records the criteria it was handed, exactly like `WorkReportQueryHandlerTests`'s own double.</summary>
    private sealed class RecordingReports : IWorkReportRepository
    {
        public WorkReportCriteria? LastCriteria { get; private set; }

        public Task<WorkReportDto> AggregateAsync(WorkReportCriteria criteria, CancellationToken ct = default)
        {
            LastCriteria = criteria;
            return Task.FromResult(new WorkReportDto(
                criteria.From, criteria.To,
                criteria.Scope.TenantWide ? WorkReportDto.ScopeTenant : WorkReportDto.ScopeScoped,
                criteria.GroupBy, WorkReportDto.EmptyBucket(null), []));
        }

        public Task<WorkReportItemsDto> ItemsAsync(WorkReportItemsCriteria criteria, CancellationToken ct = default)
        {
            LastCriteria = criteria.Report;
            return Task.FromResult(new WorkReportItemsDto(
                criteria.Bucket, criteria.Argument, criteria.GroupKey,
                criteria.Report.Scope.TenantWide ? WorkReportDto.ScopeTenant : WorkReportDto.ScopeScoped,
                0, criteria.Skip, [], false));
        }
    }
}
