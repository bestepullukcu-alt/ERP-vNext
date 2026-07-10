using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

// MOD-0288 v1 — enterprise fields persist/round-trip, the one-Primary-per-position rule, and the derived
// occupancy (Position) + derived status (Assignment) projections.
public sealed class OrganizationEnterpriseFieldsTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    // ── new fields persist + map to DTO ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Organization_unit_create_persists_enterprise_fields()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var managerPositionId = Guid.NewGuid();
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var effectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest(
            "HQ1", "Head Office", LegalEntityId, null,
            OrgUnitType: "Branch", ManagerPositionId: managerPositionId, Description: "Main branch",
            Status: "Inactive", EffectiveFrom: effectiveFrom, LocationCode: "IST", CostCenterCode: "CC-100")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var entity = Assert.Single(await orgUnits.GetAllAsync());
        Assert.Equal(OrgUnitType.Branch, entity.OrgUnitType);
        Assert.Equal(managerPositionId, entity.ManagerPositionId);
        Assert.Equal("Main branch", entity.Description);
        Assert.Equal(OrgUnitStatus.Inactive, entity.Status);
        Assert.Equal(effectiveFrom, entity.EffectiveFrom);
        Assert.Equal("IST", entity.LocationCode);
        Assert.Equal("CC-100", entity.CostCenterCode);

        var dto = TenantOrganizationMapper.ToDto(entity);
        Assert.Equal("Branch", dto.OrgUnitType);
        Assert.Equal("Inactive", dto.Status);
        Assert.Equal(managerPositionId, dto.ManagerPositionId);
    }

    [Fact]
    public async Task Position_create_persists_enterprise_fields()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        var handler = new CreatePositionCommandHandler(positions, orgUnits, TenantContext());

        var response = await handler.Handle(new CreatePositionCommand(new PositionRequest(
            "ENG1", "Engineer", org.Id, null,
            JobTitle: "Software Engineer", PositionType: "Contractor", Fte: 0.5m, Status: "Active",
            GradeCode: "G7")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var entity = Assert.Single(await positions.GetAllAsync());
        Assert.Equal("Software Engineer", entity.JobTitle);
        Assert.Equal(PositionType.Contractor, entity.PositionType);
        Assert.Equal(0.5m, entity.Fte);
        Assert.Equal(PositionStatus.Active, entity.Status);
        Assert.Equal("G7", entity.GradeCode);
    }

    [Fact]
    public async Task Assignment_create_persists_enterprise_fields_and_dto_carries_derived_status()
    {
        var positions = new InMemoryPositionRepository(TenantId);
        var position = Position("CEO", Guid.NewGuid());
        positions.Add(position);
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        // Future-dated → derived Planned.
        var from = DateTimeOffset.UtcNow.AddDays(30);
        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(
            position.Id, Guid.NewGuid(), from, null,
            AssignmentType: "Secondary", AllocationPercent: 40m, Reason: "Transfer", Notes: "Interim")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var entity = Assert.Single(await assignments.GetAllAsync());
        Assert.Equal(AssignmentType.Secondary, entity.AssignmentType);
        Assert.Equal(40m, entity.AllocationPercent);
        Assert.Equal(AssignmentReason.Transfer, entity.Reason);
        Assert.Equal("Interim", entity.Notes);

        var dto = TenantOrganizationMapper.ToDto(entity);
        Assert.Equal("Secondary", dto.AssignmentType);
        Assert.Equal("Planned", dto.DerivedStatus);
    }

    // ── one Primary per position (Secondary/Acting may overlap) ───────────────────────────────────

    [Fact]
    public async Task Two_overlapping_primary_assignments_are_rejected()
    {
        var (positions, assignments, position) = SeedPositionWithPrimary();
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(
            position.Id, Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            AssignmentType: "Primary")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Secondary_assignment_may_overlap_an_existing_primary()
    {
        var (positions, assignments, position) = SeedPositionWithPrimary();
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(
            position.Id, Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            AssignmentType: "Secondary")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    // ── derived occupancy / status ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Positions_query_derives_occupancy_from_active_assignments()
    {
        var positions = new InMemoryPositionRepository(TenantId);
        var occupied = Position("OCC", Guid.NewGuid());
        var vacant = Position("VAC", Guid.NewGuid());
        positions.Add(occupied);
        positions.Add(vacant);

        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        assignments.Add(new PositionAssignment
        {
            TenantId = TenantId,
            PositionId = occupied.Id,
            UserId = Guid.NewGuid(),
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10) // active now
        });

        var handler = new GetPositionsQueryHandler(positions, assignments);
        var response = await handler.Handle(new GetPositionsQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var occDto = response.Data!.Single(p => p.Code == "OCC");
        var vacDto = response.Data!.Single(p => p.Code == "VAC");
        Assert.False(occDto.IsVacant);
        Assert.Equal(1, occDto.ActiveAssignmentCount);
        Assert.True(vacDto.IsVacant);
        Assert.Equal(0, vacDto.ActiveAssignmentCount);
    }

    [Theory]
    [InlineData(30, null, false, "Planned")]     // starts in the future
    [InlineData(-10, null, false, "Active")]      // started, open-ended
    [InlineData(-30, -1, false, "Ended")]         // already ended
    [InlineData(-10, null, true, "Ended")]        // cancelled → Ended regardless of dates
    public void Derived_assignment_status_is_computed_from_dates_and_cancellation(int fromDays, int? toDays, bool cancelled, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        var a = new PositionAssignment
        {
            TenantId = TenantId,
            PositionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EffectiveFrom = now.AddDays(fromDays),
            EffectiveTo = toDays.HasValue ? now.AddDays(toDays.Value) : null,
            IsCancelled = cancelled
        };

        Assert.Equal(expected, TenantOrganizationMapper.DeriveStatus(a, now).ToString());
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────

    private static (InMemoryPositionRepository, InMemoryPositionAssignmentRepository, Position) SeedPositionWithPrimary()
    {
        var positions = new InMemoryPositionRepository(TenantId);
        var position = Position("CEO", Guid.NewGuid());
        positions.Add(position);
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        assignments.Add(new PositionAssignment
        {
            TenantId = TenantId,
            PositionId = position.Id,
            UserId = Guid.NewGuid(),
            AssignmentType = AssignmentType.Primary,
            EffectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EffectiveTo = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
        });
        return (positions, assignments, position);
    }

    private static TenantContext TenantContext()
    {
        var context = new TenantContext();
        context.SetTenant(TenantId);
        return context;
    }

    private static OrganizationUnit OrgUnit(string code) =>
        new() { TenantId = TenantId, Code = code, Name = code, LegalEntityId = LegalEntityId };

    private static Position Position(string code, Guid organizationUnitId) =>
        new() { TenantId = TenantId, Code = code, Name = code, OrganizationUnitId = organizationUnitId };

    private sealed class FakeLegalEntityValidator : ILegalEntityReferenceValidator
    {
        private readonly bool _referenceable;
        public FakeLegalEntityValidator(bool referenceable) => _referenceable = referenceable;

        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            Task.FromResult(_referenceable
                ? Response<LegalEntityReferenceDto>.Success(new LegalEntityReferenceDto(legalEntityId, "Legal", "Legal", "ACTIVE", true))
                : Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404));
    }

    private sealed class FakeUserReferenceValidator : IUserReferenceValidator
    {
        public Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Response<UserReferenceDto>.Success(new UserReferenceDto(userId, true)));
    }
}
