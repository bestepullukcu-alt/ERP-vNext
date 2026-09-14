using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Handlers;
using Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class VerifiedParticipantAccessTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("VER-A");

        var first = await handler.Handle(new CreateVerifiedParticipantAccessCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateVerifiedParticipantAccessCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-TENANT")), CancellationToken.None);

        var crossTenant = new GetVerifiedParticipantAccessByIdHandler(access, new FixedTenantContext(TenantB), PilotLegalEntityContext());
        var result = await crossTenant.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-ARCH")), CancellationToken.None);
        var archive = new ArchiveVerifiedParticipantAccessHandler(access, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = access.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepVerificationState.Archived, stored.VerificationState);
        Assert.Equal(TepAccessState.Archived, stored.AccessState);
    }

    [Fact]
    public async Task Verified_access_creation_fails_closed_without_association_and_policy_preconditions()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(VerifiedRequest("VER-BLOCKED", Guid.NewGuid(), Guid.NewGuid())), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Verified_access_creation_requires_same_tenant_association_dependency()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var otherTenantAssociation = ApprovedAssociation(TenantB, Guid.NewGuid());
        var sameTenantPolicy = ApprovedPolicy(TenantA);
        await association.CreateAsync(otherTenantAssociation, CancellationToken.None);
        await policy.CreateAsync(sameTenantPolicy, CancellationToken.None);
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(VerifiedRequest("VER-CROSS-ASSOC", otherTenantAssociation.Id, sameTenantPolicy.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Verified_access_creation_requires_same_tenant_policy_dependency()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var otherTenantPolicy = ApprovedPolicy(TenantB);
        var sameTenantAssociation = ApprovedAssociation(TenantA, otherTenantPolicy.Id);
        await association.CreateAsync(sameTenantAssociation, CancellationToken.None);
        await policy.CreateAsync(otherTenantPolicy, CancellationToken.None);
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(VerifiedRequest("VER-CROSS-POLICY", sameTenantAssociation.Id, otherTenantPolicy.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Verified_access_creation_succeeds_with_same_tenant_approved_dependencies()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var sameTenantPolicy = ApprovedPolicy(TenantA);
        var sameTenantAssociation = ApprovedAssociation(TenantA, sameTenantPolicy.Id);
        await association.CreateAsync(sameTenantAssociation, CancellationToken.None);
        await policy.CreateAsync(sameTenantPolicy, CancellationToken.None);
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(VerifiedRequest("VER-OK", sameTenantAssociation.Id, sameTenantPolicy.Id)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Evaluation_without_available_dependencies_returns_explicit_deferred_metadata()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.VerificationAllowed);
        Assert.Equal(TepPolicyEvaluationState.Deferred, access.Items.Single().PolicyEvaluationState);
        Assert.Equal(TepVerifiedCompanyAccessState.Deferred, access.Items.Single().VerifiedCompanyAccessState);
        Assert.Equal("Dependency evaluation deferred.", access.Items.Single().DeferredReason);
    }

    [Fact]
    public async Task Verification_request_fails_closed_when_policy_approval_missing()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-EVAL-BLOCK")), CancellationToken.None);
        var verify = new VerifyParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await verify.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Verify_path_marks_access_active_with_same_tenant_dependencies()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var sameTenantPolicy = ApprovedPolicy(TenantA);
        var sameTenantAssociation = ApprovedAssociation(TenantA, sameTenantPolicy.Id);
        await association.CreateAsync(sameTenantAssociation, CancellationToken.None);
        await policy.CreateAsync(sameTenantPolicy, CancellationToken.None);
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ReadyDeferredRequest("VER-VERIFY", sameTenantAssociation.Id, sameTenantPolicy.Id)), CancellationToken.None);
        var verify = new VerifyParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await verify.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepVerificationState.Verified, access.Items.Single().VerificationState);
        Assert.Equal(TepAccessState.Active, access.Items.Single().AccessState);
    }

    [Fact]
    public async Task Audit_metadata_is_local_deferred_only()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-AUDIT")), CancellationToken.None);
        var audit = new GetVerifiedParticipantAccessAuditMetadataHandler(access, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await audit.Handle(new(created.Data), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepLocalAuditEvidenceRetentionState.Deferred, result.Data!.LocalAuditEvidenceRetentionState);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("talent_identity")]
    [InlineData("reference_exchange")]
    [InlineData("reputation")]
    [InlineData("pii_heavy")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("VER-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_verified_participant_namespace()
    {
        Assert.NotNull(typeof(VerifiedParticipantsController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(VerifiedParticipantsController.GetAll), VerifiedParticipantPermissions.Read);
        AssertPermission(nameof(VerifiedParticipantsController.GetById), VerifiedParticipantPermissions.Read);
        AssertPermission(nameof(VerifiedParticipantsController.Create), VerifiedParticipantPermissions.Manage);
        AssertPermission(nameof(VerifiedParticipantsController.Update), VerifiedParticipantPermissions.Manage);
        AssertPermission(nameof(VerifiedParticipantsController.Archive), VerifiedParticipantPermissions.Manage);
        AssertPermission(nameof(VerifiedParticipantsController.Verify), VerifiedParticipantPermissions.Verify);
        AssertPermission(nameof(VerifiedParticipantsController.Evaluate), VerifiedParticipantPermissions.Evaluate);
        AssertPermission(nameof(VerifiedParticipantsController.GetAuditMetadata), VerifiedParticipantPermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_verified_participant_access", MongoTepVerifiedParticipantAccessRepository.CollectionName);
        Assert.Equal("ux_tep_verified_participants_tenant_code_active", MongoTepVerifiedParticipantAccessRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_verified_participants_tenant_state", MongoTepVerifiedParticipantAccessRepository.TenantStateIndexName);
    }

    [Fact]
    public async Task Create_stamps_the_selected_legal_entity()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-LE")), CancellationToken.None);
        var stored = access.Items.Single();

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task Create_without_a_permitted_legal_entity_is_forbidden()
    {
        var access = new InMemoryVerifiedParticipantAccessRepository();
        var association = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateVerifiedParticipantAccessHandler(access, association, policy, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var result = await handler.Handle(new CreateVerifiedParticipantAccessCommand(ValidRequest("VER-403")), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(access.Items);
    }

    private static VerifiedParticipantAccessRequest ValidRequest(string code) =>
        new(
            code,
            "Verified participant",
            null,
            "member-company-ref",
            "hr-participant-ref",
            "hcm-foundation-ref",
            null,
            TepVerificationState.Deferred,
            TepAccessState.Deferred,
            TepPolicyEvaluationState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepVerifiedCompanyAccessState.Deferred,
            [new TepVerifiedParticipantDependencyStateDto("tep.association-memberships", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            TepLocalAuditEvidenceRetentionState.Deferred,
            "metadata deferred");

    private static VerifiedParticipantAccessRequest ReadyDeferredRequest(string code, Guid associationId, Guid policyId) =>
        ValidRequest(code) with
        {
            AssociationMembershipId = associationId,
            ConsentVisibilityPolicyId = policyId,
            PolicyEvaluationState = TepPolicyEvaluationState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            HcmValidationState = TepShellDependencyStatus.Available,
            AssociationValidationState = TepShellDependencyStatus.Available,
            VerifiedCompanyAccessState = TepVerifiedCompanyAccessState.Ready,
            DeferredReason = null
        };

    private static VerifiedParticipantAccessRequest VerifiedRequest(string code, Guid associationId, Guid policyId) =>
        ReadyDeferredRequest(code, associationId, policyId) with
        {
            VerificationState = TepVerificationState.Verified,
            AccessState = TepAccessState.Active
        };

    private static TepAssociationMembershipRegistry ApprovedAssociation(Guid tenantId, Guid policyId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"ASSOC-{tenantId.ToString()[..8]}",
            DisplayName = "Association membership",
            AssociationMembershipState = TepAssociationMembershipState.Active,
            MemberCompanyState = TepMemberCompanyState.Verified,
            MemberCompanyReference = "member-company-ref",
            HcmFoundationReference = "hcm-foundation-ref",
            ConsentVisibilityPolicyId = policyId,
            PolicyEvaluationState = TepPolicyEvaluationState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            AssociationActivationState = TepAssociationActivationState.Active,
            SourceContractVersion = "v1",
            RegistryVersion = 1
        };

    private static TepConsentVisibilityPolicy ApprovedPolicy(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"POLICY-{tenantId.ToString()[..8]}",
            DisplayName = "Consent visibility policy",
            PolicyState = TepPolicyState.Active,
            ConsentRequirementState = TepConsentRequirementState.Approved,
            VisibilityScope = TepVisibilityScope.AssociationVisible,
            DataScopeState = TepDataScopeState.Available,
            AccessPolicyState = TepAccessPolicyState.Approved,
            AssociationConsumptionState = TepAssociationConsumptionState.ActivationApproved,
            PolicyUnavailableBehavior = TepPolicyUnavailableBehavior.FailClosed,
            SourceContractVersion = "v1",
            LocalAuditEvidenceRetentionState = TepLocalAuditEvidenceRetentionState.LocalMetadata,
            PolicyVersion = 1
        };

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(VerifiedParticipantsController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    // Fixed legal-entity ids mirroring the MDM demo hierarchy: HOLDING(root) → { MEDIKAL, TEKNOLOJI }.
    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    // Default pilot context: HOLDING selected, rolls up over the whole demo hierarchy.
    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class FixedLegalEntityContext : ILegalEntityContext
    {
        private readonly IReadOnlyCollection<Guid> _effective;
        private readonly bool _selectionAllowed;

        public FixedLegalEntityContext(
            Guid? selected,
            IReadOnlyCollection<Guid>? effective = null,
            bool? selectionAllowed = null)
        {
            SelectedLegalEntityId = selected;
            _effective = effective ?? (selected is { } s ? new[] { s } : Array.Empty<Guid>());
            _selectionAllowed = selectionAllowed ?? selected.HasValue;
        }

        public Guid? SelectedLegalEntityId { get; }

        public Task<bool> IsSelectionAllowedAsync(CancellationToken ct) => Task.FromResult(_selectionAllowed);

        public Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct) =>
            Task.FromResult(_effective);
    }

    private sealed class InMemoryVerifiedParticipantAccessRepository : ITepVerifiedParticipantAccessRepository
    {
        public List<TepVerifiedParticipantAccess> Items { get; } = [];

        public Task<IReadOnlyList<TepVerifiedParticipantAccess>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepVerifiedParticipantAccess>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepVerifiedParticipantAccess?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepVerifiedParticipantAccess access, CancellationToken ct)
        {
            Items.Add(access);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepVerifiedParticipantAccess access, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryAssociationMembershipRegistryRepository : ITepAssociationMembershipRegistryRepository
    {
        public List<TepAssociationMembershipRegistry> Items { get; } = [];

        public Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepAssociationMembershipRegistry>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct)
        {
            Items.Add(registry);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryConsentVisibilityPolicyRepository : ITepConsentVisibilityPolicyRepository
    {
        public List<TepConsentVisibilityPolicy> Items { get; } = [];

        public Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepConsentVisibilityPolicy>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct)
        {
            Items.Add(policy);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct) => Task.CompletedTask;
    }
}
