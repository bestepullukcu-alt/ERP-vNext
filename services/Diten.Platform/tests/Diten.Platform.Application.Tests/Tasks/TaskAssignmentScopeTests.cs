using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-057 — WHO may receive work, and BL-072 — why somebody is missing from the list.
///
/// <para>The defect: both assignment pickers called <c>GetAllAsync</c> on assignments, positions and units and
/// filtered on nothing but dates and archive flags. A user in one company could see and assign work to every
/// employee of every other company in the group. Listing happened to be right (position ownership narrowed it);
/// assignment was not right even by accident. Poland is inside the EU/GDPR and Turkey is not, so this is a legal
/// boundary rather than a UX preference.</para>
///
/// <para>The rule, and it is deliberately NOT the same for every picker:</para>
/// <list type="bullet">
/// <item><b>Assignee, watcher, pool position</b> — scope-limited: same legal entity, OR below me in the
/// reporting chain, OR inside a scope explicitly granted to me.</item>
/// <item><b>Approver, reviewer</b> — scope-EXEMPT. A task produced in GMG TR can legitimately be approved in
/// GMG AZ by somebody who is neither above nor below the author. That authority belongs to the PROCESS, not to
/// the user, which is why SAP resolves it through agent determination and Oracle through approval rules.
/// Applying the scope to all four pickers would silently kill intra-group approval.</item>
/// </list>
/// </summary>
public sealed class TaskAssignmentScopeTests
{
    // Two legal entities, deliberately: "same company" and "another company" are the whole subject.
    private static readonly Guid HomeLegalEntity = Guid.Parse("0a000000-0000-0000-0000-00000000000a");
    private static readonly Guid ForeignLegalEntity = Guid.Parse("0b000000-0000-0000-0000-00000000000b");

    private static readonly Guid HomeUnit = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ForeignUnit = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid MyPosition = Guid.Parse("31111111-1111-1111-1111-111111111111");
    private static readonly Guid ColleaguePosition = Guid.Parse("32222222-2222-2222-2222-222222222222");
    private static readonly Guid ForeignPosition = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ForeignReportPosition = Guid.Parse("34444444-4444-4444-4444-444444444444");
    private static readonly Guid MyBossPosition = Guid.Parse("35555555-5555-5555-5555-555555555555");

    private static readonly Guid Colleague = Guid.Parse("c0000000-0000-0000-0000-00000000000c");
    private static readonly Guid Foreigner = Guid.Parse("f0000000-0000-0000-0000-00000000000f");
    private static readonly Guid ForeignReport = Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private static readonly Guid MyBoss = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    // ── the three legs of the rule ───────────────────────────────────────────

    [Fact]
    public async Task Someone_in_MY_legal_entity_is_assignable()
    {
        var result = await People(World());

        Assert.Contains(result.People, r => r.UserId == Colleague);
    }

    [Fact]
    public async Task Someone_in_ANOTHER_legal_entity_with_no_chain_to_me_is_NOT_assignable()
    {
        // The measured defect, stated as an assertion: this person used to be offered.
        var result = await People(World());

        Assert.DoesNotContain(result.People, r => r.UserId == Foreigner);
    }

    [Fact]
    public async Task Someone_in_another_legal_entity_who_reports_UP_TO_ME_is_assignable()
    {
        /*
         * Leg (2), and the case that had never been tested anywhere: the reporting chain CROSSES the company
         * boundary on purpose. The org-unit tree cannot (ValidateParentAsync refuses a parent in another legal
         * entity) but Position.ReportsToPositionId carries no such restriction — units are the financial/legal
         * truth, the position chain is the authority truth. A group CEO assigns work to a plant manager in
         * another company; the plant's cost still books to that company.
         */
        var result = await People(World());

        Assert.Contains(result.People, r => r.UserId == ForeignReport);
    }

    [Fact]
    public async Task Being_ABOVE_me_in_the_chain_does_not_make_someone_assignable()
    {
        /*
         * Direction matters. Upward is BL-023's subject (it becomes a REQUEST, not an assignment) and is out of
         * scope here — what is asserted is only that the scope rule does not PRODUCE upward assignability by
         * accident. My boss sits in the foreign legal entity so that legs (1) and (2) both genuinely fail.
         */
        var result = await People(World());

        Assert.DoesNotContain(result.People, r => r.UserId == MyBoss);
    }

    // ── approver / reviewer are exempt ───────────────────────────────────────

    [Fact]
    public async Task The_APPROVER_list_includes_someone_from_another_company()
    {
        // Intra-group approval: GMG TR produces, GMG AZ approves. Neither above nor below, different company.
        var result = await People(World(), TaskPersonLookupPurpose.Decision);

        Assert.Contains(result.People, r => r.UserId == Foreigner);
        Assert.Contains(result.People, r => r.UserId == MyBoss);
    }

    [Fact]
    public async Task The_REVIEWER_list_is_the_same_exempt_list_as_the_approver()
    {
        // One purpose, one list: reviewing and approving are both authority questions, not scope questions.
        var decision = await People(World(), TaskPersonLookupPurpose.Decision);
        var assignment = await People(World());

        Assert.True(decision.People.Count > assignment.People.Count,
            "the decision list is not wider than the assignment list — the exemption is not applied");
        Assert.All(assignment.People, row => Assert.Contains(decision.People, d => d.UserId == row.UserId));
    }

    // ── the pool picker obeys the SAME rule ──────────────────────────────────

    [Fact]
    public async Task Pool_positions_are_filtered_by_the_same_scope()
    {
        var result = await Positions(World());

        Assert.Contains(result, r => r.PositionId == ColleaguePosition);
        Assert.DoesNotContain(result, r => r.PositionId == ForeignPosition);
        // Leg (2) again: a position below me in another company may still receive pooled work.
        Assert.Contains(result, r => r.PositionId == ForeignReportPosition);
    }

    // ── fail-closed ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_user_with_no_resolvable_scope_gets_an_EMPTY_list()
    {
        // Fail-closed is the resolver's own rule (no active assignment → no scope), and it must not degrade into
        // "show everything" here.
        var result = await People(World(), scopes: []);

        Assert.Empty(result.People);
    }

    [Fact]
    public async Task A_user_with_no_scope_is_TOLD_why_the_list_is_empty()
    {
        // Fail-closed silently is exactly the BL-072 defect. The count must reach the client.
        var result = await People(World(), scopes: []);

        Assert.True(result.Excluded.Total > 0, "an empty list reported nothing excluded");
        Assert.True(result.Excluded.OutOfScope > 0, "the out-of-scope reason was not reported");
    }

    // ── BL-072: the exclusion breakdown ──────────────────────────────────────

    [Fact]
    public async Task The_excluded_count_breaks_down_by_reason()
    {
        var world = World();
        // One more person, held out by a DIFFERENT reason: a Draft position.
        var draftPositionId = Guid.Parse("36666666-6666-6666-6666-666666666666");
        var draftHolder = Guid.Parse("d0000000-0000-0000-0000-00000000000d");
        var draft = Position(draftPositionId, HomeUnit, "Trainee");
        draft.Status = PositionStatus.Draft;

        var result = await People(world with
        {
            Positions = [.. world.Positions, draft],
            Assignments = [.. world.Assignments, Holder(draftHolder, draftPositionId)]
        });

        Assert.Equal(1, result.Excluded.PositionNotActive);
        // Foreigner and MyBoss are both out of scope; ForeignReport is in scope through the chain.
        Assert.Equal(2, result.Excluded.OutOfScope);
        Assert.Equal(3, result.Excluded.Total);
    }

    [Fact]
    public async Task The_exclusion_summary_NEVER_carries_a_name_or_an_identity()
    {
        /*
         * A security boundary, not a nicety. The whole point of the scope rule is that those people are not
         * visible; a hint that named them would hand back exactly what the rule withholds. Counts only.
         */
        var result = await People(World());

        // INSTANCE members only: what travels on the wire. A static factory constant (`None`) is not payload.
        var leaking = result.Excluded.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.PropertyType != typeof(int))
            .Select(p => p.Name)
            .ToArray();

        Assert.True(leaking.Length == 0,
            $"the exclusion summary carries non-numeric members and could leak identities: {string.Join(", ", leaking)}");
    }

    // ── the rule lives in ONE place ──────────────────────────────────────────

    [Fact]
    public void Both_pickers_resolve_scope_through_the_SAME_rule()
    {
        /*
         * Two handlers, one rule. Written twice they drift, and the drift is invisible in the direction that
         * matters: one picker narrows, the other stays wide, and work reaches somebody the product no longer
         * considers assignable. This is the reason TaskAssigneeEligibility exists and the same reason applies
         * here.
         */
        var person = typeof(GetTaskAssignmentPersonLookupHandler);
        var position = typeof(GetTaskAssignmentPositionLookupHandler);

        foreach (var handler in new[] { person, position })
        {
            Assert.Contains(
                handler.GetConstructors().Single().GetParameters(),
                p => p.ParameterType == typeof(ITaskAssignmentScopeResolver));
        }
    }

    [Fact]
    public void The_scope_rule_consumes_the_EXISTING_resolver_rather_than_a_new_engine()
    {
        // MOD-0018-FU15 already computes OrgUnit / Position / ManagerChain / LegalEntity scopes. Building a
        // second engine beside it is the K6 defect ("two places to disagree about the same truth").
        Assert.Contains(
            typeof(TaskAssignmentScopeResolver).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(IDataScopeResolver));
    }

    // ── world + helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Two companies. I hold a position in the home one. The foreign one holds three people: a stranger, my boss
    /// (up the chain) and someone who reports up to me ACROSS the company boundary.
    /// </summary>
    private static OrgWorld World() => new(
        Units:
        [
            Unit(HomeUnit, "FAC-A", "Facility A", HomeLegalEntity),
            Unit(ForeignUnit, "FAC-B", "Facility B", ForeignLegalEntity)
        ],
        Positions:
        [
            Position(MyPosition, HomeUnit, "Group CEO", reportsTo: MyBossPosition),
            Position(ColleaguePosition, HomeUnit, "Analyst"),
            Position(ForeignPosition, ForeignUnit, "Stranger"),
            // The crossing chain: plant manager (foreign company) reports to MY position.
            Position(ForeignReportPosition, ForeignUnit, "Plant Manager", reportsTo: MyPosition),
            Position(MyBossPosition, ForeignUnit, "Chairman")
        ],
        Assignments:
        [
            Holder(TaskTestData.Me, MyPosition),
            Holder(Colleague, ColleaguePosition),
            Holder(Foreigner, ForeignPosition),
            Holder(ForeignReport, ForeignReportPosition),
            Holder(MyBoss, MyBossPosition)
        ]);

    /// <summary>What MOD-0018-FU15 emits for me: my own position, my unit, my legal entity, my managers.</summary>
    private static EntitlementDataScope[] MyScopes() =>
    [
        new(EntitlementDataScopeKind.Position, MyPosition, "CEO"),
        new(EntitlementDataScopeKind.OrgUnit, HomeUnit, "FAC-A"),
        new(EntitlementDataScopeKind.LegalEntity, HomeLegalEntity, "HOME"),
        // Upward — my Chairman. Present on purpose: the rule must NOT read it as assignability.
        new(EntitlementDataScopeKind.ManagerChain, MyBossPosition, "CHAIR")
    ];

    private static async Task<AssignablePersonLookupDto> People(
        OrgWorld world,
        TaskPersonLookupPurpose purpose = TaskPersonLookupPurpose.Assignment,
        EntitlementDataScope[]? scopes = null)
    {
        var handler = new GetTaskAssignmentPersonLookupHandler(
            new FakePositionAssignmentRepository(world.Assignments),
            new FakePositionRepository(world.Positions),
            new FakeOrganizationUnitRepository(world.Units),
            new FakeUserDisplayNameResolver(),
            ScopeResolver(world, scopes));

        var response = await handler.Handle(
            new GetTaskAssignmentPersonLookupQuery("corr", purpose), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private static async Task<IReadOnlyList<AssignablePositionDto>> Positions(
        OrgWorld world, EntitlementDataScope[]? scopes = null)
    {
        var handler = new GetTaskAssignmentPositionLookupHandler(
            new FakePositionRepository(world.Positions),
            new FakeOrganizationUnitRepository(world.Units),
            new FakePositionAssignmentRepository(world.Assignments),
            ScopeResolver(world, scopes));

        var response = await handler.Handle(
            new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private static ITaskAssignmentScopeResolver ScopeResolver(OrgWorld world, EntitlementDataScope[]? scopes)
        => new TaskAssignmentScopeResolver(
            new FakeDataScopeResolver(scopes ?? MyScopes()),
            new FakePositionRepository(world.Positions),
            new FakeOrganizationUnitRepository(world.Units),
            new FakeTenantContext(TaskTestData.Tenant),
            new FakeCurrentUserContext(TaskTestData.Me));

    private sealed record OrgWorld(
        OrganizationUnit[] Units,
        Position[] Positions,
        PositionAssignment[] Assignments);

    private static PositionAssignment Holder(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
        EffectiveTo = null
    };

    private static Position Position(Guid id, Guid unitId, string name, Guid? reportsTo = null) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = name.Replace(' ', '-').ToUpperInvariant(),
        Name = name,
        OrganizationUnitId = unitId,
        ReportsToPositionId = reportsTo,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit Unit(Guid id, string code, string name, Guid legalEntityId) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = name,
        LegalEntityId = legalEntityId,
        Status = OrgUnitStatus.Active
    };
}
