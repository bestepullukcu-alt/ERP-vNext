using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class OrgDataScopeResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid LegalEntityId = Guid.Parse("ee000000-0000-0000-0000-000000000001");

    private readonly InMemoryOrganizationUnitRepository _orgUnits = new(TenantId);
    private readonly InMemoryPositionRepository _positions = new(TenantId);
    private readonly InMemoryPositionAssignmentRepository _assignments = new(TenantId);

    // Default: the Legal Entity contract reports the reference as currently referenceable.
    private OrgDataScopeResolver CreateResolver() => CreateResolver(FakeLegalEntityReferenceValidator.Referenceable());

    private OrgDataScopeResolver CreateResolver(ILegalEntityReferenceValidator legalEntityValidator) =>
        new(_orgUnits, _positions, _assignments, legalEntityValidator);

    [Fact]
    public async Task Valid_assignment_hydrates_org_position_managerchain_and_legalentity_scopes()
    {
        var rootUnit = OrgUnit("ORG-ROOT");
        var childUnit = OrgUnit("ORG-CHILD", parentId: rootUnit.Id);
        var managerPosition = Position("POS-MGR", rootUnit.Id);
        var userPosition = Position("POS-USER", rootUnit.Id, reportsTo: managerPosition.Id);
        _orgUnits.Add(rootUnit);
        _orgUnits.Add(childUnit);
        _positions.Add(managerPosition);
        _positions.Add(userPosition);
        _assignments.Add(ActiveAssignment(userPosition.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Equal(
            new[] { rootUnit.Id, childUnit.Id }.OrderBy(x => x),
            ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit).OrderBy(x => x));
        Assert.Equal(new[] { userPosition.Id }, ScopeIds(scopes, EntitlementDataScopeKind.Position));
        Assert.Equal(new[] { managerPosition.Id }, ScopeIds(scopes, EntitlementDataScopeKind.ManagerChain));
        Assert.Equal(new[] { LegalEntityId }, ScopeIds(scopes, EntitlementDataScopeKind.LegalEntity));
    }

    [Fact]
    public async Task Resolver_emits_only_the_four_v1_scope_kinds()
    {
        var unit = OrgUnit("ORG-ROOT");
        var manager = Position("POS-MGR", unit.Id);
        var userPosition = Position("POS-USER", unit.Id, reportsTo: manager.Id);
        _orgUnits.Add(unit);
        _positions.Add(manager);
        _positions.Add(userPosition);
        _assignments.Add(ActiveAssignment(userPosition.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        var emittedKinds = scopes.Select(s => s.Kind).Distinct().ToHashSet();
        Assert.Subset(
            new HashSet<EntitlementDataScopeKind>
            {
                EntitlementDataScopeKind.OrgUnit,
                EntitlementDataScopeKind.Position,
                EntitlementDataScopeKind.ManagerChain,
                EntitlementDataScopeKind.LegalEntity
            },
            emittedKinds);
        Assert.DoesNotContain(EntitlementDataScopeKind.Country, emittedKinds);
        Assert.DoesNotContain(EntitlementDataScopeKind.Own, emittedKinds);
        Assert.DoesNotContain(EntitlementDataScopeKind.Assigned, emittedKinds);
    }

    [Fact]
    public async Task No_assignment_produces_no_scope()
    {
        _orgUnits.Add(OrgUnit("ORG-ROOT"));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Expired_assignment_produces_no_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(Assignment(
            position.Id,
            effectiveFrom: DateTimeOffset.UtcNow.AddDays(-10),
            effectiveTo: DateTimeOffset.UtcNow.AddDays(-1)));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Future_assignment_produces_no_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(Assignment(position.Id, effectiveFrom: DateTimeOffset.UtcNow.AddDays(2)));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Empty_user_produces_no_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, Guid.Empty, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task OrgUnit_scope_is_own_plus_full_subtree_flat_list()
    {
        var root = OrgUnit("ORG-ROOT");
        var child = OrgUnit("ORG-CHILD", parentId: root.Id);
        var grandchild = OrgUnit("ORG-GRANDCHILD", parentId: child.Id);
        var unrelated = OrgUnit("ORG-OTHER");
        var position = Position("POS-USER", root.Id);
        _orgUnits.Add(root);
        _orgUnits.Add(child);
        _orgUnits.Add(grandchild);
        _orgUnits.Add(unrelated);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        var orgScopeIds = ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit).ToHashSet();
        Assert.Contains(root.Id, orgScopeIds);
        Assert.Contains(child.Id, orgScopeIds);
        Assert.Contains(grandchild.Id, orgScopeIds);
        Assert.DoesNotContain(unrelated.Id, orgScopeIds);
    }

    [Fact]
    public async Task ManagerChain_emits_position_ids_up_the_reporting_chain_not_org_units()
    {
        var unit = OrgUnit("ORG-ROOT");
        var top = Position("POS-TOP", unit.Id);
        var middle = Position("POS-MID", unit.Id, reportsTo: top.Id);
        var userPosition = Position("POS-USER", unit.Id, reportsTo: middle.Id);
        _orgUnits.Add(unit);
        _positions.Add(top);
        _positions.Add(middle);
        _positions.Add(userPosition);
        _assignments.Add(ActiveAssignment(userPosition.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        var managerChain = ScopeIds(scopes, EntitlementDataScopeKind.ManagerChain).ToHashSet();
        Assert.Equal(new HashSet<Guid> { middle.Id, top.Id }, managerChain);
        Assert.DoesNotContain(userPosition.Id, managerChain);
        Assert.DoesNotContain(unit.Id, managerChain);
    }

    [Fact]
    public async Task ManagerChain_is_cycle_safe()
    {
        var unit = OrgUnit("ORG-ROOT");
        var positionA = Position("POS-A", unit.Id);
        var positionB = Position("POS-B", unit.Id, reportsTo: positionA.Id);
        positionA.ReportsToPositionId = positionB.Id; // A -> B -> A cycle
        var userPosition = Position("POS-USER", unit.Id, reportsTo: positionA.Id);
        _orgUnits.Add(unit);
        _positions.Add(positionA);
        _positions.Add(positionB);
        _positions.Add(userPosition);
        _assignments.Add(ActiveAssignment(userPosition.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        var managerChain = ScopeIds(scopes, EntitlementDataScopeKind.ManagerChain).ToHashSet();
        // Terminates without infinite loop; both cycle members captured exactly once.
        Assert.Equal(new HashSet<Guid> { positionA.Id, positionB.Id }, managerChain);
    }

    [Fact]
    public async Task Archived_position_produces_no_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id, archived: true);
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Archived_org_unit_is_excluded_from_subtree()
    {
        var root = OrgUnit("ORG-ROOT");
        var archivedChild = OrgUnit("ORG-CHILD", parentId: root.Id, archived: true);
        var position = Position("POS-USER", root.Id);
        _orgUnits.Add(root);
        _orgUnits.Add(archivedChild);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        var orgScopeIds = ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit).ToHashSet();
        Assert.Contains(root.Id, orgScopeIds);
        Assert.DoesNotContain(archivedChild.Id, orgScopeIds);
    }

    [Fact]
    public async Task Cross_tenant_assignment_is_isolated()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        _orgUnits.Add(unit);
        _positions.Add(position);
        // Assignment belongs to a different tenant; must never contribute scope.
        _assignments.Add(new PositionAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = OtherTenantId,
            PositionId = position.Id,
            UserId = UserId,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            EffectiveTo = null
        });

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Multiple_active_positions_union_their_scopes()
    {
        var unitA = OrgUnit("ORG-A");
        var unitB = OrgUnit("ORG-B");
        var positionA = Position("POS-A", unitA.Id);
        var positionB = Position("POS-B", unitB.Id);
        _orgUnits.Add(unitA);
        _orgUnits.Add(unitB);
        _positions.Add(positionA);
        _positions.Add(positionB);
        _assignments.Add(ActiveAssignment(positionA.Id));
        _assignments.Add(ActiveAssignment(positionB.Id));

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Equal(
            new[] { positionA.Id, positionB.Id }.OrderBy(x => x),
            ScopeIds(scopes, EntitlementDataScopeKind.Position).OrderBy(x => x));
        Assert.Equal(
            new[] { unitA.Id, unitB.Id }.OrderBy(x => x),
            ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit).OrderBy(x => x));
    }

    [Fact]
    public async Task Soft_deleted_assignment_produces_no_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        var assignment = ActiveAssignment(position.Id);
        assignment.IsDeleted = true;
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(assignment);

        var scopes = await CreateResolver().ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task Referenceable_legal_entity_emits_legalentity_scope()
    {
        var unit = OrgUnit("ORG-ROOT");
        var position = Position("POS-USER", unit.Id);
        _orgUnits.Add(unit);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        var validator = FakeLegalEntityReferenceValidator.Referenceable();
        var scopes = await CreateResolver(validator).ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Equal(new[] { LegalEntityId }, ScopeIds(scopes, EntitlementDataScopeKind.LegalEntity));
        Assert.True(validator.CallCount >= 1); // the contract was actually consulted, not read off the org unit
    }

    [Fact]
    public async Task Non_referenceable_legal_entity_is_not_emitted_but_other_scopes_remain()
    {
        var unit = OrgUnit("ORG-ROOT");
        var manager = Position("POS-MGR", unit.Id);
        var position = Position("POS-USER", unit.Id, reportsTo: manager.Id);
        _orgUnits.Add(unit);
        _positions.Add(manager);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        // Simulates archived / deleted / cross-tenant / Referenceable!=true / id-mismatch / non-2xx — the validator
        // collapses all of these to an unsuccessful response.
        var scopes = await CreateResolver(FakeLegalEntityReferenceValidator.NotReferenceable())
            .ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(ScopeIds(scopes, EntitlementDataScopeKind.LegalEntity));
        Assert.Contains(unit.Id, ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit));
        Assert.Equal(new[] { position.Id }, ScopeIds(scopes, EntitlementDataScopeKind.Position));
        Assert.Equal(new[] { manager.Id }, ScopeIds(scopes, EntitlementDataScopeKind.ManagerChain));
    }

    [Fact]
    public async Task Validator_failure_does_not_emit_legalentity_and_preserves_other_scopes()
    {
        var unit = OrgUnit("ORG-ROOT");
        var manager = Position("POS-MGR", unit.Id);
        var position = Position("POS-USER", unit.Id, reportsTo: manager.Id);
        _orgUnits.Add(unit);
        _positions.Add(manager);
        _positions.Add(position);
        _assignments.Add(ActiveAssignment(position.Id));

        // Validator throws (timeout / network / JSON) → fail-closed, no LegalEntity scope, other scopes preserved.
        var scopes = await CreateResolver(FakeLegalEntityReferenceValidator.Throwing())
            .ResolveAsync(TenantId, UserId, string.Empty, null, CancellationToken.None);

        Assert.Empty(ScopeIds(scopes, EntitlementDataScopeKind.LegalEntity));
        Assert.Contains(unit.Id, ScopeIds(scopes, EntitlementDataScopeKind.OrgUnit));
        Assert.Equal(new[] { position.Id }, ScopeIds(scopes, EntitlementDataScopeKind.Position));
        Assert.Equal(new[] { manager.Id }, ScopeIds(scopes, EntitlementDataScopeKind.ManagerChain));
    }

    private static IEnumerable<Guid> ScopeIds(IEnumerable<EntitlementDataScope> scopes, EntitlementDataScopeKind kind) =>
        scopes.Where(s => s.Kind == kind && s.ScopeId.HasValue).Select(s => s.ScopeId!.Value);

    private static OrganizationUnit OrgUnit(string code, Guid? parentId = null, bool archived = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Code = code,
        Name = code,
        LegalEntityId = LegalEntityId,
        ParentOrganizationUnitId = parentId,
        IsArchived = archived
    };

    private static Position Position(string code, Guid organizationUnitId, Guid? reportsTo = null, bool archived = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Code = code,
        Name = code,
        OrganizationUnitId = organizationUnitId,
        ReportsToPositionId = reportsTo,
        IsArchived = archived
    };

    private static PositionAssignment ActiveAssignment(Guid positionId) =>
        Assignment(positionId, DateTimeOffset.UtcNow.AddDays(-1));

    private static PositionAssignment Assignment(Guid positionId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        PositionId = positionId,
        UserId = UserId,
        EffectiveFrom = effectiveFrom,
        EffectiveTo = effectiveTo
    };

    private sealed class FakeLegalEntityReferenceValidator : ILegalEntityReferenceValidator
    {
        private readonly Func<Guid, Task<Response<LegalEntityReferenceDto>>> _responder;

        private FakeLegalEntityReferenceValidator(Func<Guid, Task<Response<LegalEntityReferenceDto>>> responder) =>
            _responder = responder;

        public int CallCount { get; private set; }

        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
        {
            CallCount++;
            return _responder(legalEntityId);
        }

        public static FakeLegalEntityReferenceValidator Referenceable() =>
            new(id => Task.FromResult(Response<LegalEntityReferenceDto>.Success(
                new LegalEntityReferenceDto(id, "Legal", "Legal", "ACTIVE", true))));

        public static FakeLegalEntityReferenceValidator NotReferenceable() =>
            new(_ => Task.FromResult(Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404)));

        public static FakeLegalEntityReferenceValidator Throwing() =>
            new(_ => throw new HttpRequestException("simulated network failure"));
    }
}
