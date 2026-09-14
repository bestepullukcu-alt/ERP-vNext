using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class TrustLevelTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var handler = CreateHandler(policies, TenantA);
        var request = ValidRequest("TRUST-A");

        var first = await handler.Handle(new CreateTrustLevelPolicyCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateTrustLevelPolicyCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var created = await CreateHandler(policies, TenantA)
            .Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-TENANT")), CancellationToken.None);
        var query = new GetTrustLevelPolicyByIdHandler(policies, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var created = await CreateHandler(policies, TenantA)
            .Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-ARCH")), CancellationToken.None);
        var archive = new ArchiveTrustLevelPolicyHandler(policies, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = policies.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepTrustLevelPolicyState.Archived, stored.TrustLevelPolicyState);
    }

    [Fact]
    public async Task Activating_create_fails_closed_when_association_dependency_is_cross_tenant()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var consentPolicies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantB, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        var reviewCase = ApprovedReviewCase(TenantA, association.Id, policy.Id, verifiedAccess.Id);
        await consentPolicies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        await reviewCases.CreateAsync(reviewCase, CancellationToken.None);
        var handler = new CreateTrustLevelPolicyHandler(policies, associations, consentPolicies, verified, reviewCases, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateTrustLevelPolicyCommand(ActivatingRequest("TRUST-CROSS", association.Id, policy.Id, verifiedAccess.Id, reviewCase.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Evaluation_without_available_preconditions_returns_explicit_deferred_metadata()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var created = await CreateHandler(policies, TenantA)
            .Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateTrustLevelPolicyHandler(
            policies,
            new InMemoryAssociationMembershipRegistryRepository(),
            new InMemoryConsentVisibilityPolicyRepository(),
            new InMemoryVerifiedParticipantAccessRepository(),
            new InMemoryReviewBoardCaseMetadataRepository(),
            new FixedTenantContext(TenantA),
            PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.TrustElevationAllowed);
        Assert.Equal(TepTrustLevelPolicyState.Deferred, policies.Items.Single().TrustLevelPolicyState);
        Assert.Equal(TepTrustValidationState.Deferred, policies.Items.Single().TrustValidationState);
        Assert.Equal("Trust-level dependency evaluation deferred.", policies.Items.Single().DeferredReason);
    }

    [Fact]
    public async Task Trust_elevation_request_fails_closed_when_multi_signature_policy_unavailable()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var created = await CreateHandler(policies, TenantA)
            .Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-MSIG-DEFER") with
            {
                MultiSignatureRequirementState = TepMultiSignatureRequirementState.Deferred,
                MultiSignaturePolicyUnavailableBehavior = TepMultiSignaturePolicyUnavailableBehavior.DeferredEvaluation
            }), CancellationToken.None);
        var evaluate = new EvaluateTrustLevelPolicyHandler(
            policies,
            new InMemoryAssociationMembershipRegistryRepository(),
            new InMemoryConsentVisibilityPolicyRepository(),
            new InMemoryVerifiedParticipantAccessRepository(),
            new InMemoryReviewBoardCaseMetadataRepository(),
            new FixedTenantContext(TenantA),
            PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Trust_elevation_succeeds_with_same_tenant_approved_preconditions()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var consentPolicies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        var reviewCase = ApprovedReviewCase(TenantA, association.Id, policy.Id, verifiedAccess.Id);
        await consentPolicies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        await reviewCases.CreateAsync(reviewCase, CancellationToken.None);
        var create = new CreateTrustLevelPolicyHandler(policies, associations, consentPolicies, verified, reviewCases, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-OK") with
        {
            AssociationMembershipRegistryId = association.Id,
            ConsentVisibilityPolicyId = policy.Id,
            VerifiedParticipantAccessId = verifiedAccess.Id,
            ReviewBoardCaseId = reviewCase.Id,
            AssociationValidationState = TepShellDependencyStatus.Available,
            ConsentVisibilityValidationState = TepShellDependencyStatus.Available,
            VerifiedAccessValidationState = TepShellDependencyStatus.Available,
            ReviewBoardValidationState = TepShellDependencyStatus.Available,
            SignatureSubstrateState = TepSignatureSubstrateState.Available,
            LegalSecurityTrustModelState = TepTrustLegalSecurityState.Approved,
            MultiSignatureRequirementState = TepMultiSignatureRequirementState.Approved,
            MultiSignaturePolicyUnavailableBehavior = TepMultiSignaturePolicyUnavailableBehavior.FailClosed
        }), CancellationToken.None);
        var evaluate = new EvaluateTrustLevelPolicyHandler(policies, associations, consentPolicies, verified, reviewCases, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.TrustElevationAllowed);
        Assert.Equal(TepTrustLevelPolicyState.Active, policies.Items.Single().TrustLevelPolicyState);
        Assert.Equal(TepTrustValidationState.Approved, policies.Items.Single().TrustValidationState);
    }

    [Fact]
    public async Task Audit_metadata_is_local_deferred_only()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var created = await CreateHandler(policies, TenantA)
            .Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-AUDIT")), CancellationToken.None);
        var audit = new GetTrustLevelPolicyAuditMetadataHandler(policies, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await audit.Handle(new(created.Data), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepSignatureSubstrateState.Deferred, result.Data!.SignatureSubstrateState);
        Assert.Equal(TepAuditEvidenceState.Deferred, result.Data.AuditEvidenceState);
        Assert.Equal(TepRetentionState.Deferred, result.Data.RetentionState);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("talent_identity")]
    [InlineData("reference_exchange")]
    [InlineData("signature_execution")]
    [InlineData("cryptographic")]
    [InlineData("pii_heavy")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var handler = CreateHandler(policies, TenantA);
        var request = ValidRequest("TRUST-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateTrustLevelPolicyCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_trust_level_namespace()
    {
        Assert.NotNull(typeof(TrustLevelsController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(TrustLevelsController.GetAll), TrustLevelPermissions.Read);
        AssertPermission(nameof(TrustLevelsController.GetById), TrustLevelPermissions.Read);
        AssertPermission(nameof(TrustLevelsController.Create), TrustLevelPermissions.Manage);
        AssertPermission(nameof(TrustLevelsController.Update), TrustLevelPermissions.Manage);
        AssertPermission(nameof(TrustLevelsController.Archive), TrustLevelPermissions.Manage);
        AssertPermission(nameof(TrustLevelsController.Evaluate), TrustLevelPermissions.Evaluate);
        AssertPermission(nameof(TrustLevelsController.GetAuditMetadata), TrustLevelPermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_trust_level_policies", MongoTepTrustLevelPolicyMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_trust_level_policies_tenant_code_active", MongoTepTrustLevelPolicyMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_trust_level_policies_tenant_state", MongoTepTrustLevelPolicyMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public async Task Create_stamps_the_selected_legal_entity()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var handler = CreateHandler(policies, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-LE")), CancellationToken.None);
        var stored = policies.Items.Single();

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task Create_without_a_permitted_legal_entity_is_forbidden()
    {
        var policies = new InMemoryTrustLevelPolicyMetadataRepository();
        var handler = CreateHandler(policies, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var result = await handler.Handle(new CreateTrustLevelPolicyCommand(ValidRequest("TRUST-403")), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(policies.Items);
    }

    private static CreateTrustLevelPolicyHandler CreateHandler(InMemoryTrustLevelPolicyMetadataRepository repository, Guid tenantId, ILegalEntityContext? legalEntityContext = null) =>
        new(
            repository,
            new InMemoryAssociationMembershipRegistryRepository(),
            new InMemoryConsentVisibilityPolicyRepository(),
            new InMemoryVerifiedParticipantAccessRepository(),
            new InMemoryReviewBoardCaseMetadataRepository(),
            new FixedTenantContext(tenantId),
            legalEntityContext ?? PilotLegalEntityContext());

    private static TrustLevelPolicyRequest ValidRequest(string code) =>
        new(
            code,
            "Trust policy metadata",
            TepTrustLevelPolicyState.Deferred,
            TepTrustValidationState.Deferred,
            TepMultiSignatureRequirementState.Deferred,
            TepMultiSignaturePolicyUnavailableBehavior.DeferredEvaluation,
            null,
            null,
            null,
            null,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepSignatureSubstrateState.Deferred,
            "signature-substrate-reference",
            TepTrustLegalSecurityState.Deferred,
            TepAuditEvidenceState.Deferred,
            TepRetentionState.Deferred,
            [new TrustLevelDependencyStateDto("tep.association-memberships", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static TrustLevelPolicyRequest ActivatingRequest(string code, Guid associationId, Guid policyId, Guid verifiedAccessId, Guid reviewCaseId) =>
        ValidRequest(code) with
        {
            TrustLevelPolicyState = TepTrustLevelPolicyState.Active,
            TrustValidationState = TepTrustValidationState.Approved,
            MultiSignatureRequirementState = TepMultiSignatureRequirementState.Approved,
            MultiSignaturePolicyUnavailableBehavior = TepMultiSignaturePolicyUnavailableBehavior.FailClosed,
            AssociationMembershipRegistryId = associationId,
            ConsentVisibilityPolicyId = policyId,
            VerifiedParticipantAccessId = verifiedAccessId,
            ReviewBoardCaseId = reviewCaseId,
            AssociationValidationState = TepShellDependencyStatus.Available,
            ConsentVisibilityValidationState = TepShellDependencyStatus.Available,
            VerifiedAccessValidationState = TepShellDependencyStatus.Available,
            ReviewBoardValidationState = TepShellDependencyStatus.Available,
            SignatureSubstrateState = TepSignatureSubstrateState.Available,
            LegalSecurityTrustModelState = TepTrustLegalSecurityState.Approved,
            DeferredReason = null
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

    private static TepVerifiedParticipantAccess ApprovedVerifiedAccess(Guid tenantId, Guid associationId, Guid policyId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"VER-{tenantId.ToString()[..8]}",
            DisplayName = "Verified participant",
            AssociationMembershipId = associationId,
            MemberCompanyReference = "member-company-ref",
            HrParticipantReference = "hr-participant-ref",
            HcmFoundationReference = "hcm-foundation-ref",
            ConsentVisibilityPolicyId = policyId,
            VerificationState = TepVerificationState.Verified,
            AccessState = TepAccessState.Active,
            PolicyEvaluationState = TepPolicyEvaluationState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            HcmValidationState = TepShellDependencyStatus.Available,
            AssociationValidationState = TepShellDependencyStatus.Available,
            VerifiedCompanyAccessState = TepVerifiedCompanyAccessState.Ready,
            SourceContractVersion = "v1",
            VerificationVersion = 1
        };

    private static TepReviewBoardCaseMetadata ApprovedReviewCase(Guid tenantId, Guid associationId, Guid policyId, Guid verifiedAccessId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"REV-{tenantId.ToString()[..8]}",
            DisplayName = "Review case",
            ReviewBoardCaseState = TepReviewBoardCaseState.DecisionRecorded,
            ReviewDecisionState = TepReviewDecisionState.Approved,
            AssociationMembershipRegistryId = associationId,
            ConsentVisibilityPolicyId = policyId,
            VerifiedParticipantAccessId = verifiedAccessId,
            ReviewerEligibilityState = TepReviewerEligibilityState.Eligible,
            SegregationOfDutiesState = TepSegregationOfDutiesState.Passed,
            LegalSecurityDecisionState = TepLegalSecurityDecisionState.Approved,
            ExternalReviewBoardState = TepExternalReviewBoardState.NotRequired,
            AuditEvidenceState = TepAuditEvidenceState.Deferred,
            RetentionState = TepRetentionState.Deferred,
            SourceContractVersion = "v1",
            ReviewBoardVersion = 1
        };

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(TrustLevelsController)
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

    private sealed class InMemoryTrustLevelPolicyMetadataRepository : ITepTrustLevelPolicyMetadataRepository
    {
        public List<TepTrustLevelPolicyMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepTrustLevelPolicyMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepTrustLevelPolicyMetadata>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepTrustLevelPolicyMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct) => Task.CompletedTask;
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

    private sealed class InMemoryReviewBoardCaseMetadataRepository : ITepReviewBoardCaseMetadataRepository
    {
        public List<TepReviewBoardCaseMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepReviewBoardCaseMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepReviewBoardCaseMetadata>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepReviewBoardCaseMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }
}
