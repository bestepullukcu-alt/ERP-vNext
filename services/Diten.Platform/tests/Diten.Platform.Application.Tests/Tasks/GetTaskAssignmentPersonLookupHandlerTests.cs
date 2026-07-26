using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 §K6.4 — who may receive a task. Replaces the bare text input that demanded a user GUID.
///
/// <para>Assignability comes from holding a position, so the interesting cases are the ones where an assignment
/// exists but should NOT count (expired, cancelled, on a draft/archived position), and the ones where a name
/// cannot be resolved.</para>
/// </summary>
public sealed class GetTaskAssignmentPersonLookupHandlerTests
{
    private static readonly Guid UnitId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUnitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PositionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherPositionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task A_person_holding_an_active_position_is_assignable()
    {
        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId)],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist")],
            units: [Unit(UnitId, "FAC-A", "Facility A")],
            names: [(TaskTestData.Me, "Selin Aras")]);

        var row = Assert.Single(result);
        Assert.Equal(TaskTestData.Me, row.UserId);
        Assert.Equal("Selin Aras", row.DisplayName);
        Assert.Equal("QA Specialist", row.PositionName);
        // The unit label is what tells two same-position people apart.
        Assert.Equal("Facility A", row.OrganizationUnitName);
        Assert.Equal("FAC-A", row.OrganizationUnitCode);
    }

    [Fact]
    public async Task An_expired_assignment_does_not_make_someone_assignable()
    {
        var expired = Holder(TaskTestData.Me, PositionId);
        expired.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);   // half-open interval: already over

        var result = await Run(
            assignments: [expired],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist")],
            units: [Unit(UnitId, "FAC-A", "Facility A")]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_cancelled_assignment_does_not_make_someone_assignable()
    {
        var cancelled = Holder(TaskTestData.Me, PositionId);
        cancelled.IsCancelled = true;

        var result = await Run(
            assignments: [cancelled],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist")],
            units: [Unit(UnitId, "FAC-A", "Facility A")]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_future_dated_assignment_is_not_yet_active()
    {
        var future = Holder(TaskTestData.Me, PositionId);
        future.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(7);

        var result = await Run(
            assignments: [future],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist")],
            units: [Unit(UnitId, "FAC-A", "Facility A")]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_draft_or_archived_position_does_not_make_its_holder_assignable()
    {
        var draft = ActivePosition(PositionId, UnitId, "QA Specialist");
        draft.Status = PositionStatus.Draft;

        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId)],
            positions: [draft],
            units: [Unit(UnitId, "FAC-A", "Facility A")]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Two_people_in_the_same_position_are_distinguishable_by_unit()
    {
        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId), Holder(TaskTestData.Rival, OtherPositionId)],
            positions:
            [
                ActivePosition(PositionId, UnitId, "QA Specialist"),
                ActivePosition(OtherPositionId, OtherUnitId, "QA Specialist")
            ],
            units: [Unit(UnitId, "FAC-A", "Facility A"), Unit(OtherUnitId, "FAC-B", "Facility B")],
            names: [(TaskTestData.Me, "Selin Aras"), (TaskTestData.Rival, "Deniz Koç")]);

        Assert.Equal(2, result.Count);
        // Same position title, different facility — the row must carry enough to tell them apart.
        Assert.Equal(new[] { "Facility A", "Facility B" }, result.Select(r => r.OrganizationUnitName).OrderBy(n => n));
    }

    [Fact]
    public async Task Someone_holding_two_positions_appears_once()
    {
        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId), Holder(TaskTestData.Me, OtherPositionId)],
            positions:
            [
                ActivePosition(PositionId, UnitId, "QA Specialist"),
                ActivePosition(OtherPositionId, OtherUnitId, "Auditor")
            ],
            units: [Unit(UnitId, "FAC-A", "Facility A"), Unit(OtherUnitId, "FAC-B", "Facility B")]);

        Assert.Single(result);
    }

    // ── Name resolution is best effort ────────────────────────────────────────

    [Fact]
    public async Task The_lookup_still_works_when_AuthService_is_unreachable()
    {
        var resolver = new FakeUserDisplayNameResolver { Unavailable = true };

        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId)],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist")],
            units: [Unit(UnitId, "FAC-A", "Facility A")],
            resolver: resolver);

        // Degraded, not broken: the person is still selectable by position + unit.
        var row = Assert.Single(result);
        Assert.Null(row.DisplayName);
        Assert.Equal("QA Specialist", row.PositionName);
        Assert.Equal("Facility A", row.OrganizationUnitName);
    }

    [Fact]
    public async Task Names_are_resolved_in_ONE_batched_call_not_one_per_person()
    {
        var people = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        var positions = people.Select((_, i) => ActivePosition(PositionFor(i), UnitId, $"Role {i}")).ToArray();
        var assignments = people.Select((user, i) => Holder(user, PositionFor(i))).ToArray();
        var resolver = new FakeUserDisplayNameResolver();

        var result = await Run(assignments, positions, [Unit(UnitId, "FAC-A", "Facility A")], resolver: resolver);

        Assert.Equal(20, result.Count);
        // 20 people, ONE resolve call — the N+1 this seam exists to prevent.
        Assert.Single(resolver.Calls);
        Assert.Equal(20, resolver.Calls[0].Count);
    }

    [Fact]
    public async Task A_person_whose_name_resolves_is_listed_before_one_whose_name_does_not()
    {
        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId), Holder(TaskTestData.Rival, OtherPositionId)],
            positions:
            [
                ActivePosition(PositionId, UnitId, "QA Specialist"),
                ActivePosition(OtherPositionId, UnitId, "Auditor")
            ],
            units: [Unit(UnitId, "FAC-A", "Facility A")],
            names: [(TaskTestData.Rival, "Deniz Koç")]);

        Assert.Equal(2, result.Count);
        Assert.Equal("Deniz Koç", result[0].DisplayName);
        Assert.Null(result[1].DisplayName);
    }


    [Fact]
    public async Task Another_tenants_people_are_never_listed()
    {
        // The repository doubles mirror the tenant execution filter, so a foreign assignment/position/unit is
        // simply not returned — the lookup cannot leak a person from another tenant.
        var foreignAssignment = new PositionAssignment
        {
            TenantId = TaskTestData.OtherTenant,
            PositionId = OtherPositionId,
            UserId = TaskTestData.Rival,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
        };
        var foreignPosition = new Position
        {
            Id = OtherPositionId,
            TenantId = TaskTestData.OtherTenant,
            Code = "FOREIGN",
            Name = "Foreign Role",
            OrganizationUnitId = OtherUnitId,
            Status = PositionStatus.Active
        };
        var foreignUnit = new OrganizationUnit
        {
            Id = OtherUnitId,
            TenantId = TaskTestData.OtherTenant,
            Code = "FAC-X",
            Name = "Foreign Facility",
            LegalEntityId = Guid.NewGuid(),
            Status = OrgUnitStatus.Active
        };

        var result = await Run(
            assignments: [Holder(TaskTestData.Me, PositionId), foreignAssignment],
            positions: [ActivePosition(PositionId, UnitId, "QA Specialist"), foreignPosition],
            units: [Unit(UnitId, "FAC-A", "Facility A"), foreignUnit],
            names: [(TaskTestData.Me, "Selin Aras"), (TaskTestData.Rival, "Foreign Person")]);

        var row = Assert.Single(result);
        Assert.Equal(TaskTestData.Me, row.UserId);
        Assert.DoesNotContain(result, r => r.UserId == TaskTestData.Rival);
        Assert.DoesNotContain(result, r => r.OrganizationUnitName == "Foreign Facility");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Guid PositionFor(int index)
        => Guid.Parse($"55555555-5555-5555-5555-{index:D12}");

    private static async Task<IReadOnlyList<AssignablePersonDto>> Run(
        PositionAssignment[] assignments,
        Position[] positions,
        OrganizationUnit[] units,
        (Guid Id, string Name)[]? names = null,
        FakeUserDisplayNameResolver? resolver = null)
    {
        var handler = new GetTaskAssignmentPersonLookupHandler(
            new FakePositionAssignmentRepository(assignments),
            new FakePositionRepository(positions),
            new FakeOrganizationUnitRepository(units),
            resolver ?? new FakeUserDisplayNameResolver(names ?? []));

        var response = await handler.Handle(
            new GetTaskAssignmentPersonLookupQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private static PositionAssignment Holder(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
        EffectiveTo = null
    };

    private static Position ActivePosition(Guid id, Guid unitId, string name) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = name.Replace(' ', '-').ToUpperInvariant(),
        Name = name,
        OrganizationUnitId = unitId,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit Unit(Guid id, string code, string name) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = name,
        LegalEntityId = Guid.NewGuid(),
        Status = OrgUnitStatus.Active
    };
}
