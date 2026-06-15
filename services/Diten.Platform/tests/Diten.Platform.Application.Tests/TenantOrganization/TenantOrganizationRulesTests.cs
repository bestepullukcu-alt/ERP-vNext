using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.TenantOrganization.Validators;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

public sealed class TenantOrganizationRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Organization_unit_create_rejects_missing_legal_entity()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(false), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest("ROOT", "Root", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_create_rejects_duplicate_code_in_tenant()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        orgUnits.Add(OrgUnit("ROOT"));
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest(" root ", "Root 2", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_create_rejects_normalized_empty_code()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest("!!!", "Root", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_update_rejects_normalized_empty_code()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var handler = new UpdateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true));

        var response = await handler.Handle(new UpdateOrganizationUnitCommand(org.Id, new OrganizationUnitRequest("---", "Root", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_create_rejects_cross_tenant_parent_fail_closed()
    {
        var parent = new OrganizationUnit { TenantId = OtherTenantId, Code = "PARENT", Name = "Parent", LegalEntityId = LegalEntityId };
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        orgUnits.Add(parent);
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest("CHILD", "Child", LegalEntityId, parent.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_create_rejects_orphan_parent()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest("CHILD", "Child", LegalEntityId, Guid.NewGuid())), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_update_rejects_cross_legal_entity_parent()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var child = OrgUnit("CHILD");
        var parent = OrgUnit("PARENT", legalEntityId: Guid.NewGuid());
        orgUnits.Add(child);
        orgUnits.Add(parent);
        var handler = new UpdateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true));

        var response = await handler.Handle(new UpdateOrganizationUnitCommand(child.Id, new OrganizationUnitRequest("CHILD", "Child", LegalEntityId, parent.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Organization_unit_update_rejects_cycle()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var parent = OrgUnit("PARENT");
        var child = OrgUnit("CHILD", parentId: parent.Id);
        orgUnits.Add(parent);
        orgUnits.Add(child);
        var handler = new UpdateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true));

        var response = await handler.Handle(new UpdateOrganizationUnitCommand(parent.Id, new OrganizationUnitRequest("PARENT", "Parent", LegalEntityId, child.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Position_create_rejects_missing_org_unit()
    {
        var positions = new InMemoryPositionRepository(TenantId);
        var handler = new CreatePositionCommandHandler(positions, new InMemoryOrganizationUnitRepository(TenantId), TenantContext());

        var response = await handler.Handle(new CreatePositionCommand(new PositionRequest("CEO", "CEO", Guid.NewGuid(), null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Position_create_rejects_normalized_empty_code()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        var handler = new CreatePositionCommandHandler(positions, orgUnits, TenantContext());

        var response = await handler.Handle(new CreatePositionCommand(new PositionRequest("!!!", "CEO", org.Id, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Position_update_rejects_normalized_empty_code()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        var position = Position("CEO", org.Id);
        positions.Add(position);
        var handler = new UpdatePositionCommandHandler(positions, orgUnits);

        var response = await handler.Handle(new UpdatePositionCommand(position.Id, new PositionRequest("---", "CEO", org.Id, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Position_create_rejects_duplicate_code_in_tenant()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        positions.Add(Position("CEO", org.Id));
        var handler = new CreatePositionCommandHandler(positions, orgUnits, TenantContext());

        var response = await handler.Handle(new CreatePositionCommand(new PositionRequest(" ceo ", "Chief Executive", org.Id, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Position_create_rejects_cross_tenant_org_unit_fail_closed()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var otherTenantOrgUnit = new OrganizationUnit { TenantId = OtherTenantId, Code = "ROOT", Name = "Root", LegalEntityId = LegalEntityId };
        orgUnits.Add(otherTenantOrgUnit);
        var positions = new InMemoryPositionRepository(TenantId);
        var handler = new CreatePositionCommandHandler(positions, orgUnits, TenantContext());

        var response = await handler.Handle(new CreatePositionCommand(new PositionRequest("CEO", "CEO", otherTenantOrgUnit.Id, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Position_create_rejects_self_reports_to()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        var existing = Position("CEO", org.Id);
        positions.Add(existing);
        var handler = new UpdatePositionCommandHandler(positions, orgUnits);

        var response = await handler.Handle(new UpdatePositionCommand(existing.Id, new PositionRequest("CEO", "CEO", org.Id, existing.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Position_update_rejects_reporting_cycle()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        orgUnits.Add(org);
        var positions = new InMemoryPositionRepository(TenantId);
        var manager = Position("MGR", org.Id);
        var worker = Position("WRK", org.Id, manager.Id);
        positions.Add(manager);
        positions.Add(worker);
        var handler = new UpdatePositionCommandHandler(positions, orgUnits);

        var response = await handler.Handle(new UpdatePositionCommand(manager.Id, new PositionRequest("MGR", "Manager", org.Id, worker.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void Position_assignment_rejects_invalid_date_range_through_validator()
    {
        var validator = new CreatePositionAssignmentCommandValidator();
        var request = new PositionAssignmentRequest(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1));

        var result = validator.Validate(new CreatePositionAssignmentCommand(request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Position_assignment_rejects_missing_position()
    {
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        var handler = new CreatePositionAssignmentCommandHandler(assignments, new InMemoryPositionRepository(TenantId), TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Position_assignment_rejects_cross_tenant_position_fail_closed()
    {
        var positions = new InMemoryPositionRepository(TenantId);
        var otherTenantPosition = new Position { TenantId = OtherTenantId, Code = "CEO", Name = "CEO", OrganizationUnitId = Guid.NewGuid() };
        positions.Add(otherTenantPosition);
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(otherTenantPosition.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Position_assignment_rejects_overlap_for_same_position()
    {
        var org = OrgUnit("ROOT");
        var position = Position("CEO", org.Id);
        var positions = new InMemoryPositionRepository(TenantId);
        positions.Add(position);
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        assignments.Add(new PositionAssignment
        {
            TenantId = TenantId,
            PositionId = position.Id,
            UserId = Guid.NewGuid(),
            EffectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EffectiveTo = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
        });
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(
            position.Id,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero))), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Position_assignment_accepts_same_user_on_different_positions()
    {
        var org = OrgUnit("ROOT");
        var positionA = Position("A", org.Id);
        var positionB = Position("B", org.Id);
        var positions = new InMemoryPositionRepository(TenantId);
        positions.Add(positionA);
        positions.Add(positionB);
        var assignments = new InMemoryPositionAssignmentRepository(TenantId);
        var userId = Guid.NewGuid();
        assignments.Add(new PositionAssignment
        {
            TenantId = TenantId,
            PositionId = positionA.Id,
            UserId = userId,
            EffectiveFrom = DateTimeOffset.UtcNow
        });
        var handler = new CreatePositionAssignmentCommandHandler(assignments, positions, TenantContext(), new FakeUserReferenceValidator());

        var response = await handler.Handle(new CreatePositionAssignmentCommand(new PositionAssignmentRequest(positionB.Id, userId, DateTimeOffset.UtcNow, null)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public async Task Manager_chain_returns_derived_single_line()
    {
        var org = OrgUnit("ROOT");
        var director = Position("DIR", org.Id);
        var manager = Position("MGR", org.Id, director.Id);
        var worker = Position("WRK", org.Id, manager.Id);
        var positions = new InMemoryPositionRepository(TenantId);
        positions.Add(director);
        positions.Add(manager);
        positions.Add(worker);
        var handler = new GetManagerChainQueryHandler(positions);

        var response = await handler.Handle(new GetManagerChainQuery(worker.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal([manager.Id, director.Id], response.Data!.Chain.Select(x => x.PositionId));
    }

    [Fact]
    public async Task Manager_chain_cycle_fails_closed()
    {
        var org = OrgUnit("ROOT");
        var a = Position("A", org.Id);
        var b = Position("B", org.Id, a.Id);
        a.ReportsToPositionId = b.Id;
        var positions = new InMemoryPositionRepository(TenantId);
        positions.Add(a);
        positions.Add(b);
        var handler = new GetManagerChainQueryHandler(positions);

        var response = await handler.Handle(new GetManagerChainQuery(a.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Manager_chain_depth_greater_than_32_fails_closed()
    {
        var org = OrgUnit("ROOT");
        var positions = new InMemoryPositionRepository(TenantId);
        Position? previous = null;
        for (var i = 0; i < 34; i++)
        {
            var position = Position($"P{i}", org.Id, previous?.Id);
            positions.Add(position);
            previous = position;
        }

        var handler = new GetManagerChainQueryHandler(positions);

        var response = await handler.Handle(new GetManagerChainQuery(previous!.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Archived_record_mutation_is_rejected()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        org.IsArchived = true;
        orgUnits.Add(org);
        var handler = new UpdateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true));

        var response = await handler.Handle(new UpdateOrganizationUnitCommand(org.Id, new OrganizationUnitRequest("ROOT", "Root", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_record_mutation_fails_closed()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var org = OrgUnit("ROOT");
        org.IsDeleted = true;
        orgUnits.Add(org);
        var handler = new UpdateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true));

        var response = await handler.Handle(new UpdateOrganizationUnitCommand(org.Id, new OrganizationUnitRequest("ROOT", "Root", LegalEntityId, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_id_is_server_side_only_for_create()
    {
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var handler = new CreateOrganizationUnitCommandHandler(orgUnits, new FakeLegalEntityValidator(true), TenantContext());

        var response = await handler.Handle(new CreateOrganizationUnitCommand(new OrganizationUnitRequest("ROOT", "Root", LegalEntityId, null)), CancellationToken.None);
        var created = await orgUnits.GetByIdAsync(response.Data);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TenantId, created!.TenantId);
    }

    [Fact]
    public void Request_payloads_do_not_expose_tenant_id()
    {
        Assert.DoesNotContain(typeof(OrganizationUnitRequest).GetProperties(), x => x.Name == "TenantId");
        Assert.DoesNotContain(typeof(PositionRequest).GetProperties(), x => x.Name == "TenantId");
        Assert.DoesNotContain(typeof(PositionAssignmentRequest).GetProperties(), x => x.Name == "TenantId");
    }

    private static TenantContext TenantContext()
    {
        var context = new TenantContext();
        context.SetTenant(TenantId);
        return context;
    }

    private static OrganizationUnit OrgUnit(string code, Guid? legalEntityId = null, Guid? parentId = null) =>
        new()
        {
            TenantId = TenantId,
            Code = code,
            Name = code,
            LegalEntityId = legalEntityId ?? LegalEntityId,
            ParentOrganizationUnitId = parentId
        };

    private static Position Position(string code, Guid organizationUnitId, Guid? reportsToPositionId = null) =>
        new()
        {
            TenantId = TenantId,
            Code = code,
            Name = code,
            OrganizationUnitId = organizationUnitId,
            ReportsToPositionId = reportsToPositionId
        };

    private sealed class FakeLegalEntityValidator : ILegalEntityReferenceValidator
    {
        private readonly bool _referenceable;

        public FakeLegalEntityValidator(bool referenceable) => _referenceable = referenceable;

        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
        {
            var response = _referenceable
                ? Response<LegalEntityReferenceDto>.Success(new LegalEntityReferenceDto(legalEntityId, "Legal", "Legal", "ACTIVE", true))
                : Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404);

            return Task.FromResult(response);
        }
    }

    private sealed class FakeUserReferenceValidator : IUserReferenceValidator
    {
        private readonly bool _referenceable;

        public FakeUserReferenceValidator(bool referenceable = true) => _referenceable = referenceable;

        public Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default)
        {
            var response = _referenceable
                ? Response<UserReferenceDto>.Success(new UserReferenceDto(userId, true))
                : Response<UserReferenceDto>.Fail("User is not referenceable.", 404);

            return Task.FromResult(response);
        }
    }
}
