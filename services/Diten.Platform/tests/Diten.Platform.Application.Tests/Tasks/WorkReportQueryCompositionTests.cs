using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// THE QUERY THE REPORT ACTUALLY SENDS — rendered, and read.
///
/// <para><b>⚠ WHY THIS FILE EXISTS, AND WHAT IT REPLACES.</b> The intersection rule — a filter narrows the
/// caller's scope and can never replace it — was covered only by <c>WorkReportFilterTests</c>, whose
/// <c>Pipeline</c> helper composes <c>InScope</c> then <c>MatchesFilter</c> ITSELF. That proves the test's
/// composition. Production composes somewhere else entirely: as Mongo filter terms inside
/// <c>WorkReportRepository</c>, which no test was building.</para>
///
/// <para>CONTROL TOWER demonstrated the gap by editing the repository to drop the scope whenever a filter was
/// present — <c>hasNarrowing ? Filter.Empty : scoped</c>, the plausible "they asked for one unit, so query that
/// unit directly" — and the whole suite stayed GREEN at 47/47. A privilege-escalation seam that renders
/// perfectly and passes every test is the worst shape a defect can take.</para>
///
/// <para><b>What this watches instead.</b> <c>WorkReportRepository.BuildMatchFilter</c> is the one composition
/// production runs. These render it to BSON — deterministic, no Mongo process needed — and assert the tenant
/// and scope clauses are present in the query IN EVERY CASE, including every case where a filter was supplied.
/// Reading the rendered document is the only way to be sure of what the database will be asked.</para>
/// </summary>
public sealed class WorkReportQueryCompositionTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Me = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid MyUnit = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherUnit = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Stranger = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Company = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The caller's own scope — one unit, the ordinary case.</summary>
    private static WorkReportScope ScopedToMyUnit() =>
        WorkReportScope.FromDataScopes(
            [new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, MyUnit, null)], Me);

    private static WorkReportCriteria Criteria(WorkReportFilter? filter = null, WorkReportScope? scope = null) =>
        new(From, To, scope ?? ScopedToMyUnit(), WorkReportGroupBy.None, filter);

    /// <summary>
    /// The filter as the DRIVER will send it. Rendering is deterministic and needs no server, so this reads the
    /// real query rather than a description of it.
    /// </summary>
    private static BsonDocument Render(WorkReportCriteria criteria) =>
        WorkReportRepository
            .BuildMatchFilter(Tenant, criteria)
            .Render(
                BsonSerializer.SerializerRegistry.GetSerializer<TaskItem>(),
                BsonSerializer.SerializerRegistry);

    private static string Json(WorkReportCriteria criteria) => Render(criteria).ToJson();

    // ── THE GUARD ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_rendered_query_ALWAYS_carries_the_tenant_and_the_scope_whatever_was_filtered()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED, and the one CONTROL TOWER's sabotage walked straight past.
         *
         * Every filter combination a reader can express, each rendered and read. The tenant clause and the
         * caller's scoped unit have to appear in ALL of them: the moment a filter causes the scope to be
         * swapped out, one of these queries stops naming the unit and this fails.
         */
        WorkReportFilter?[] filters =
        [
            null,
            new WorkReportFilter(),
            new WorkReportFilter(OrganizationUnitId: MyUnit),
            new WorkReportFilter(OrganizationUnitId: OtherUnit),
            new WorkReportFilter(AssigneeUserId: Me),
            new WorkReportFilter(AssigneeUserId: Stranger),
            new WorkReportFilter(LegalEntityId: Company),
            new WorkReportFilter(TaskTypeCode: "DEV"),
            new WorkReportFilter(Priority: TaskPriority.High),
            new WorkReportFilter(OrganizationUnitId: OtherUnit, AssigneeUserId: Stranger, Priority: TaskPriority.Low)
        ];

        foreach (var filter in filters)
        {
            var json = Json(Criteria(filter));

            Assert.True(
                json.Contains(Tenant.ToString(), StringComparison.OrdinalIgnoreCase),
                $"the TENANT clause vanished from the rendered query for filter <{Describe(filter)}>: {json}");

            Assert.True(
                json.Contains(MyUnit.ToString(), StringComparison.OrdinalIgnoreCase),
                $"the SCOPE clause vanished from the rendered query for filter <{Describe(filter)}> — a filter "
                + $"has replaced the caller's scope instead of narrowing it: {json}");
        }
    }

    [Fact]
    public void Asking_about_a_unit_OUTSIDE_my_scope_renders_BOTH_units_so_the_query_can_match_nothing()
    {
        /*
         * The intersection, visible in the query text. A reader naming a unit they may not see produces
         * `scope = MyUnit  AND  organizationUnitId = OtherUnit` — two clauses that cannot both hold, so Mongo
         * returns nothing. That is the correct answer, and it is correct BECAUSE both clauses are there.
         *
         * A query carrying only `OtherUnit` would return that unit's work. That is the failure this names.
         */
        var json = Json(Criteria(new WorkReportFilter(OrganizationUnitId: OtherUnit)));

        Assert.Contains(MyUnit.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(OtherUnit.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_filter_only_ever_ADDS_clauses_to_the_unfiltered_query()
    {
        /*
         * The property stated structurally: whatever the unfiltered query asked for is still asked for once a
         * filter is applied. Removal is the only way to widen an ANDed query, so "nothing was removed" is the
         * whole safety argument — and it is checked here rather than reasoned about.
         */
        var unfiltered = Render(Criteria());
        var filtered = Render(Criteria(new WorkReportFilter(AssigneeUserId: Stranger, Priority: TaskPriority.High)));

        var before = unfiltered["$and"].AsBsonArray.Select(x => x.ToJson()).ToList();
        var after = filtered["$and"].AsBsonArray.Select(x => x.ToJson()).ToList();

        Assert.All(before, clause =>
            Assert.True(after.Contains(clause), $"the filtered query dropped a clause the unfiltered one had: {clause}"));
        Assert.True(after.Count > before.Count, "a filter was supplied but added no clause at all");
    }

    // ── NON-VACUITY: this test can actually see a missing scope ──────────────────────────────────────────

    [Fact]
    public void The_guard_can_TELL_a_scoped_query_from_an_unscoped_one()
    {
        /*
         * ⚠ WITHOUT THIS, THE ASSERTIONS ABOVE COULD BE READING A STRING THAT ALWAYS CONTAINS EVERYTHING.
         *
         * A tenant-wide scope legitimately names no unit — so if the rendered text of a tenant-wide query
         * ALSO contained the unit id, the guard would be matching on something other than the scope and would
         * stay green through the very edit it exists to catch.
         */
        var scoped = Json(Criteria());
        var tenantWide = Json(Criteria(scope: WorkReportScope.TenantWideScope()));

        Assert.Contains(MyUnit.ToString(), scoped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(MyUnit.ToString(), tenantWide, StringComparison.OrdinalIgnoreCase);

        // Both still name the tenant — that clause is unconditional.
        Assert.Contains(Tenant.ToString(), scoped, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Tenant.ToString(), tenantWide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_empty_scope_renders_a_query_that_cannot_match_anything()
    {
        /*
         * Fail-closed, at the query. The handler short-circuits before reaching the repository and the
         * repository checks again — this is the third lock: even if both were bypassed, the QUERY itself
         * refuses. An `$or` over zero branches is TRUE in Mongo, which is the accident that would make an
         * unscoped report look perfectly normal.
         */
        var json = Json(Criteria(scope: WorkReportScope.Empty));

        Assert.Contains(Tenant.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_id", json);   // the impossible clause the scope builder falls back to
        Assert.DoesNotContain(MyUnit.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_period_is_in_the_query_too_so_the_report_is_never_a_full_scan()
    {
        // The other bound this report depends on. A rendered query with no date term is a full-collection scan
        // wearing a date picker, which the criteria refuse and this confirms actually reaches the driver.
        var rendered = Render(Criteria());

        /*
         * ⚠ READ FROM THE DOCUMENT, NOT FROM ITS TEXT. The first version of this asserted on the string
         * "2026-06-01" and failed: the driver renders a DateTimeOffset as a document of ticks and offset, not
         * as an ISO string. Matching on rendered text would have made the guard depend on a serializer's
         * formatting choice rather than on the clause being there.
         */
        /*
         * ⚠ EVERY FIELD, GATHERED RECURSIVELY — not by walking to a position in the tree.
         *
         * The second attempt here looked for "the `$or` term" and threw: there are TWO. The SCOPE renders as an
         * `$or` as well, which is itself worth knowing — it is exactly why "an `$or` over zero branches is TRUE
         * in Mongo" is a hazard this repository guards against elsewhere. A guard that hard-codes a path
         * breaks whenever the shape moves; one that asks "is this field asked about anywhere" does not.
         */
        var fields = FieldNames(rendered).ToList();

        Assert.Contains(nameof(TaskItem.CreatedAt), fields);
        Assert.Contains(nameof(TaskItem.CompletedAt), fields);
        Assert.Contains(nameof(TaskItem.CancelledAt), fields);
    }

    /// <summary>Every field name the rendered query mentions, at any depth.</summary>
    private static IEnumerable<string> FieldNames(BsonValue value)
    {
        if (value is BsonDocument document)
        {
            foreach (var element in document)
            {
                // `$and`, `$or`, `$gte` … are operators, not fields; their VALUES still hold fields.
                if (!element.Name.StartsWith('$'))
                {
                    yield return element.Name;
                }

                foreach (var nested in FieldNames(element.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (value is BsonArray array)
        {
            foreach (var nested in array.SelectMany(FieldNames))
            {
                yield return nested;
            }
        }
    }

    [Fact]
    public void The_production_assembly_holds_no_SECOND_in_memory_scope_rule()
    {
        /*
         * ⚠ THE DRIFT GUARD FOR THE MOVE ITSELF.
         *
         * `InScope` shipped in `WorkReportTally` with zero production callers while every one of its 15 call
         * sites was a test — so the tested copy and the ENFORCING copy were different code, which is how
         * CONTROL TOWER's sabotage passed 47 green tests. It now lives in `WorkReportScopeMirror`, in this
         * assembly, where its only users are.
         *
         * This fails if it comes back: an in-memory scope predicate in the production tally would mean the
         * report had two answers to "whose rows may this reader see" again, and only one of them shipped.
         */
        var tally = typeof(WorkReportTally)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain("InScope", tally);

        // Non-vacuity: the type is really there and really has the members this asserts about.
        Assert.Contains(nameof(WorkReportTally.MatchesFilter), tally);
        Assert.Contains(nameof(WorkReportTally.Build), tally);
    }

    private static string Describe(WorkReportFilter? filter) =>
        filter is null
            ? "null"
            : $"le={filter.LegalEntityId} ou={filter.OrganizationUnitId} as={filter.AssigneeUserId} "
              + $"tt={filter.TaskTypeCode} pr={filter.Priority}";
}
