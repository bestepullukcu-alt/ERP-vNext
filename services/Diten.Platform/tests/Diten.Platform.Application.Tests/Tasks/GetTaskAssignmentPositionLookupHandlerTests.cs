using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the assignable-position lookup exists to prevent the two-facility failure: a pool picker that shows
// only "QA Specialist" cannot tell Facility A's position from Facility B's, so work silently reaches the wrong
// site. It must carry the organization unit label and must exclude positions that are not real yet.
public sealed class GetTaskAssignmentPositionLookupHandlerTests
{
    private static readonly Guid UnitA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnitB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LegalEntityB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Same_named_positions_in_two_facilities_are_distinguishable()
    {
        var qaAtA = Position("QA-A", "QA Specialist", UnitA);
        var qaAtB = Position("QA-B", "QA Specialist", UnitB);

        var response = await Handler(
            positions: [qaAtA, qaAtB],
            units: [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA), Unit(UnitB, "FAC-B", "Facility B", LegalEntityB)])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var rows = response.Data!;
        Assert.Equal(2, rows.Count);

        // The position NAME alone is ambiguous — the unit label is what disambiguates it.
        Assert.All(rows, r => Assert.Equal("QA Specialist", r.PositionName));
        Assert.Contains(rows, r => r.OrganizationUnitName == "Facility A" && r.OrganizationUnitCode == "FAC-A");
        Assert.Contains(rows, r => r.OrganizationUnitName == "Facility B" && r.OrganizationUnitCode == "FAC-B");

        // The legal entity travels too: facilities can be separate legal entities.
        Assert.Contains(rows, r => r.LegalEntityId == LegalEntityA);
        Assert.Contains(rows, r => r.LegalEntityId == LegalEntityB);
    }

    [Fact]
    public async Task Draft_positions_are_excluded_because_Position_status_defaults_to_Draft()
    {
        var draft = Position("QA-DRAFT", "Draft QA", UnitA);
        draft.Status = PositionStatus.Draft;
        var active = Position("QA-OK", "Active QA", UnitA);

        var response = await Handler([draft, active], [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA)])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        var row = Assert.Single(response.Data!);
        Assert.Equal("Active QA", row.PositionName);
    }

    [Theory]
    [InlineData(PositionStatus.Frozen)]
    [InlineData(PositionStatus.Closed)]
    public async Task Non_active_positions_are_excluded(PositionStatus status)
    {
        var position = Position("QA-X", "Some QA", UnitA);
        position.Status = status;

        var response = await Handler([position], [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA)])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task Archived_positions_and_archived_units_are_excluded()
    {
        var archivedPosition = Position("QA-ARCH", "Archived QA", UnitA);
        archivedPosition.IsArchived = true;

        var inArchivedUnit = Position("QA-U", "QA in archived unit", UnitB);
        var archivedUnit = Unit(UnitB, "FAC-B", "Facility B", LegalEntityB);
        archivedUnit.IsArchived = true;

        var response = await Handler(
            [archivedPosition, inArchivedUnit],
            [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA), archivedUnit])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task A_position_whose_unit_cannot_be_resolved_is_skipped_not_shown_unlabelled()
    {
        // Showing it without a facility label is exactly how work reaches the wrong site.
        var orphan = Position("QA-ORPHAN", "Orphan QA", Guid.NewGuid());

        var response = await Handler([orphan], [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA)])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task Active_holder_count_uses_the_half_open_interval()
    {
        var position = Position("QA-A", "QA Specialist", UnitA);
        var now = DateTimeOffset.UtcNow;

        var current = Assignment(position.Id, TaskTestData.Me, now.AddDays(-1), null);
        var expired = Assignment(position.Id, TaskTestData.Rival, now.AddDays(-10), now.AddDays(-1));
        var future = Assignment(position.Id, Guid.NewGuid(), now.AddDays(5), null);
        var cancelled = Assignment(position.Id, Guid.NewGuid(), now.AddDays(-2), null);
        cancelled.IsCancelled = true;

        var response = await Handler(
            [position],
            [Unit(UnitA, "FAC-A", "Facility A", LegalEntityA)],
            [current, expired, future, cancelled])
            .Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None);

        var row = Assert.Single(response.Data!);
        Assert.Equal(1, row.ActiveHolderCount);
    }

    private static GetTaskAssignmentPositionLookupHandler Handler(
        Position[] positions,
        OrganizationUnit[] units,
        PositionAssignment[]? assignments = null)
        => new(
            new FakePositionRepository(positions),
            new FakeOrganizationUnitRepository(units),
            new FakePositionAssignmentRepository(assignments ?? []));

    private static Position Position(string code, string name, Guid unitId) => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = name,
        OrganizationUnitId = unitId,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit Unit(Guid id, string code, string name, Guid legalEntityId) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = name,
        LegalEntityId = legalEntityId
    };

    private static PositionAssignment Assignment(Guid positionId, Guid userId, DateTimeOffset from, DateTimeOffset? to)
        => new()
        {
            TenantId = TaskTestData.Tenant,
            PositionId = positionId,
            UserId = userId,
            EffectiveFrom = from,
            EffectiveTo = to
        };
}
