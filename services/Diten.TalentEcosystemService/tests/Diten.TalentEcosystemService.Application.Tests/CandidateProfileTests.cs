using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Handlers;
using Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class CandidateProfileTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA);
        var request = ValidRequest("CAND-A");

        var first = await handler.Handle(new CreateCandidateProfileCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateCandidateProfileCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-TENANT")), CancellationToken.None);
        var query = new GetCandidateProfileByIdHandler(repos.Candidates, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-ARCH")), CancellationToken.None);
        var archive = new ArchiveCandidateProfileHandler(repos.Candidates, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repos.Candidates.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepCandidateIdentityState.Archived, stored.CandidateIdentityState);
        Assert.Equal(TepTalentProfileState.Archived, stored.TalentProfileState);
    }

    [Fact]
    public async Task Activating_create_fails_closed_when_consent_policy_is_cross_tenant()
    {
        var repos = new Repositories();
        var deps = await SeedApprovedDependenciesAsync(repos, TenantA);
        var crossTenantPolicy = ApprovedPolicy(TenantB);
        await repos.Policies.CreateAsync(crossTenantPolicy, CancellationToken.None);
        var handler = CreateHandler(repos, TenantA);

        var result = await handler.Handle(new CreateCandidateProfileCommand(ActiveRequest("CAND-CROSS", deps) with
        {
            ConsentVisibilityPolicyId = crossTenantPolicy.Id
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Activating_update_fails_closed_when_trust_dependency_is_missing()
    {
        var repos = new Repositories();
        var deps = await SeedApprovedDependenciesAsync(repos, TenantA);
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-UPD")), CancellationToken.None);
        var update = new UpdateCandidateProfileHandler(
            repos.Candidates,
            repos.Associations,
            repos.Policies,
            repos.Verified,
            repos.Reviews,
            repos.Trust,
            new FixedTenantContext(TenantA),
            PilotLegalEntityContext());

        var result = await update.Handle(new(created.Data, ActiveRequest("CAND-UPD", deps) with
        {
            TrustLevelPolicyId = Guid.NewGuid()
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Non_activating_evaluation_without_dependencies_returns_deferred_metadata()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-DEFER")), CancellationToken.None);
        var evaluate = EvaluateHandler(repos, TenantA);

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ActivationAllowed);
        Assert.Equal(TepCandidateIdentityState.Deferred, repos.Candidates.Items.Single().CandidateIdentityState);
        Assert.Equal(TepTalentProfileState.Deferred, repos.Candidates.Items.Single().TalentProfileState);
        Assert.Equal("Candidate profile dependency evaluation deferred.", repos.Candidates.Items.Single().DeferredReason);
    }

    [Fact]
    public async Task Activation_evaluation_succeeds_with_same_tenant_approved_dependencies()
    {
        var repos = new Repositories();
        var deps = await SeedApprovedDependenciesAsync(repos, TenantA);
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ActiveRequest("CAND-OK", deps) with
            {
                CandidateIdentityState = TepCandidateIdentityState.Deferred,
                TalentProfileState = TepTalentProfileState.Deferred
            }), CancellationToken.None);
        var evaluate = EvaluateHandler(repos, TenantA);

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.ActivationAllowed);
        Assert.Equal(TepCandidateIdentityState.Active, repos.Candidates.Items.Single().CandidateIdentityState);
        Assert.Equal(TepTalentProfileState.Published, repos.Candidates.Items.Single().TalentProfileState);
    }

    [Theory]
    [InlineData("raw_profile_body")]
    [InlineData("national_id")]
    [InlineData("dateofbirth")]
    [InlineData("home_address")]
    [InlineData("credential_secret")]
    [InlineData("provider_payload")]
    [InlineData("pii_heavy")]
    [InlineData("reference_exchange")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA);
        var request = ValidRequest("CAND-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateCandidateProfileCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Audit_metadata_is_local_deferred_only()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-AUDIT")), CancellationToken.None);
        var audit = new GetCandidateProfileAuditMetadataHandler(repos.Candidates, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await audit.Handle(new(created.Data), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepLocalAuditEvidenceRetentionState.Deferred, result.Data!.LocalAuditEvidenceRetentionState);
        Assert.Single(result.Data.DependencyStates);
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.DoesNotContain(typeof(CandidateProfileRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public void Runtime_contract_exposes_pack_section_4_talent_profile_metadata_fields()
    {
        var requestProperties = typeof(CandidateProfileRequest).GetProperties().Select(property => property.Name).ToHashSet();
        var entityProperties = typeof(TepCandidateProfileMetadata).GetProperties().Select(property => property.Name).ToHashSet();

        foreach (var field in new[]
        {
            "ProfileCompletenessState",
            "CredentialSummaryMetadata",
            "VisibilityClassification",
            "ProfilePolicyEvaluationState",
            "VerifiedParticipantId"
        })
        {
            Assert.Contains(field, requestProperties);
            Assert.Contains(field, entityProperties);
        }

        Assert.DoesNotContain("VerifiedParticipantAccessId", requestProperties);
        Assert.DoesNotContain("VerifiedParticipantAccessId", entityProperties);
    }

    [Fact]
    public void Controller_permissions_match_candidate_profile_namespace()
    {
        Assert.NotNull(typeof(CandidateProfilesController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(CandidateProfilesController.GetAll), CandidateProfilePermissions.Read);
        AssertPermission(nameof(CandidateProfilesController.GetById), CandidateProfilePermissions.Read);
        AssertPermission(nameof(CandidateProfilesController.Create), CandidateProfilePermissions.Manage);
        AssertPermission(nameof(CandidateProfilesController.Update), CandidateProfilePermissions.Manage);
        AssertPermission(nameof(CandidateProfilesController.Archive), CandidateProfilePermissions.Manage);
        AssertPermission(nameof(CandidateProfilesController.Evaluate), CandidateProfilePermissions.Evaluate);
        AssertPermission(nameof(CandidateProfilesController.GetAuditMetadata), CandidateProfilePermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_candidate_profiles", MongoTepCandidateProfileMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_candidate_profiles_tenant_code_active", MongoTepCandidateProfileMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_candidate_profiles_tenant_state", MongoTepCandidateProfileMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public async Task Create_stamps_the_selected_legal_entity()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-LE")), CancellationToken.None);
        var stored = repos.Candidates.Items.Single();

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task Create_without_a_permitted_legal_entity_is_forbidden()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var result = await handler.Handle(new CreateCandidateProfileCommand(ValidRequest("CAND-403")), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(repos.Candidates.Items);
    }

    private static CreateCandidateProfileHandler CreateHandler(Repositories repos, Guid tenantId, ILegalEntityContext? legalEntityContext = null) =>
        new(repos.Candidates, repos.Associations, repos.Policies, repos.Verified, repos.Reviews, repos.Trust, new FixedTenantContext(tenantId), legalEntityContext ?? PilotLegalEntityContext());

    private static EvaluateCandidateProfileHandler EvaluateHandler(Repositories repos, Guid tenantId) =>
        new(repos.Candidates, repos.Associations, repos.Policies, repos.Verified, repos.Reviews, repos.Trust, new FixedTenantContext(tenantId), PilotLegalEntityContext());

    private static CandidateProfileRequest ValidRequest(string code) =>
        new(
            code,
            "Candidate metadata",
            "candidate-ref",
            "profile-ref",
            null,
            null,
            null,
            null,
            null,
            "hcm-foundation-ref",
            "skill summary metadata",
            "credential summary metadata",
            "experience summary metadata",
            TepCandidateIdentityState.Deferred,
            TepTalentProfileState.Deferred,
            TepProfileCompletenessState.Deferred,
            TepCandidateVisibilityClassification.Private,
            TepCandidateConsentBasisState.Deferred,
            TepPolicyEvaluationState.Deferred,
            TepPolicyEvaluationState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepDataScopeState.Deferred,
            TepDataMinimizationState.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            TepShellDependencyStatus.Deferred,
            [new CandidateProfileDependencyStateDto("tep.consent-visibility-policies", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            TepLocalAuditEvidenceRetentionState.Deferred,
            "metadata deferred");

    private static CandidateProfileRequest ActiveRequest(string code, DependencyIds deps) =>
        ValidRequest(code) with
        {
            AssociationMembershipId = deps.AssociationId,
            ConsentVisibilityPolicyId = deps.PolicyId,
            VerifiedParticipantId = deps.VerifiedId,
            ReviewBoardCaseId = deps.ReviewId,
            TrustLevelPolicyId = deps.TrustId,
            CandidateIdentityState = TepCandidateIdentityState.Active,
            TalentProfileState = TepTalentProfileState.Published,
            ProfileCompletenessState = TepProfileCompletenessState.Complete,
            VisibilityClassification = TepCandidateVisibilityClassification.AssociationVisible,
            ConsentBasisState = TepCandidateConsentBasisState.Approved,
            PolicyEvaluationState = TepPolicyEvaluationState.Approved,
            ProfilePolicyEvaluationState = TepPolicyEvaluationState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            DataScopeState = TepDataScopeState.Available,
            DataMinimizationState = TepDataMinimizationState.Approved,
            AssociationValidationState = TepShellDependencyStatus.Available,
            VerifiedAccessValidationState = TepShellDependencyStatus.Available,
            ReviewBoardValidationState = TepShellDependencyStatus.Available,
            TrustLevelValidationState = TepShellDependencyStatus.Available,
            HcmValidationState = TepShellDependencyStatus.Available,
            LocalAuditEvidenceRetentionState = TepLocalAuditEvidenceRetentionState.LocalMetadata,
            DeferredReason = null
        };

    private static async Task<DependencyIds> SeedApprovedDependenciesAsync(Repositories repos, Guid tenantId)
    {
        var policy = ApprovedPolicy(tenantId);
        var association = ApprovedAssociation(tenantId, policy.Id);
        var verified = ApprovedVerifiedAccess(tenantId, association.Id, policy.Id);
        var review = ApprovedReviewCase(tenantId, association.Id, policy.Id, verified.Id);
        var trust = ApprovedTrustLevel(tenantId, association.Id, policy.Id, verified.Id, review.Id);
        await repos.Policies.CreateAsync(policy, CancellationToken.None);
        await repos.Associations.CreateAsync(association, CancellationToken.None);
        await repos.Verified.CreateAsync(verified, CancellationToken.None);
        await repos.Reviews.CreateAsync(review, CancellationToken.None);
        await repos.Trust.CreateAsync(trust, CancellationToken.None);
        return new(association.Id, policy.Id, verified.Id, review.Id, trust.Id);
    }

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

    private static TepReviewBoardCaseMetadata ApprovedReviewCase(Guid tenantId, Guid associationId, Guid policyId, Guid verifiedId) =>
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
            VerifiedParticipantAccessId = verifiedId,
            ReviewerEligibilityState = TepReviewerEligibilityState.Eligible,
            SegregationOfDutiesState = TepSegregationOfDutiesState.Passed,
            LegalSecurityDecisionState = TepLegalSecurityDecisionState.Approved,
            ExternalReviewBoardState = TepExternalReviewBoardState.NotRequired,
            AuditEvidenceState = TepAuditEvidenceState.Deferred,
            RetentionState = TepRetentionState.Deferred,
            SourceContractVersion = "v1",
            ReviewBoardVersion = 1
        };

    private static TepTrustLevelPolicyMetadata ApprovedTrustLevel(Guid tenantId, Guid associationId, Guid policyId, Guid verifiedId, Guid reviewId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"TRUST-{tenantId.ToString()[..8]}",
            DisplayName = "Trust level policy",
            TrustLevelPolicyState = TepTrustLevelPolicyState.Active,
            TrustValidationState = TepTrustValidationState.Approved,
            MultiSignatureRequirementState = TepMultiSignatureRequirementState.Approved,
            MultiSignaturePolicyUnavailableBehavior = TepMultiSignaturePolicyUnavailableBehavior.FailClosed,
            AssociationMembershipRegistryId = associationId,
            ConsentVisibilityPolicyId = policyId,
            VerifiedParticipantAccessId = verifiedId,
            ReviewBoardCaseId = reviewId,
            AssociationValidationState = TepShellDependencyStatus.Available,
            ConsentVisibilityValidationState = TepShellDependencyStatus.Available,
            VerifiedAccessValidationState = TepShellDependencyStatus.Available,
            ReviewBoardValidationState = TepShellDependencyStatus.Available,
            SignatureSubstrateState = TepSignatureSubstrateState.Available,
            LegalSecurityTrustModelState = TepTrustLegalSecurityState.Approved,
            AuditEvidenceState = TepAuditEvidenceState.Deferred,
            RetentionState = TepRetentionState.Deferred,
            SourceContractVersion = "v1",
            TrustPolicyVersion = 1
        };

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(CandidateProfilesController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private sealed record DependencyIds(Guid AssociationId, Guid PolicyId, Guid VerifiedId, Guid ReviewId, Guid TrustId);

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

    private sealed class Repositories
    {
        public InMemoryCandidateProfileMetadataRepository Candidates { get; } = new();
        public InMemoryAssociationMembershipRegistryRepository Associations { get; } = new();
        public InMemoryConsentVisibilityPolicyRepository Policies { get; } = new();
        public InMemoryVerifiedParticipantAccessRepository Verified { get; } = new();
        public InMemoryReviewBoardCaseMetadataRepository Reviews { get; } = new();
        public InMemoryTrustLevelPolicyMetadataRepository Trust { get; } = new();
    }

    private sealed class InMemoryCandidateProfileMetadataRepository : ITepCandidateProfileMetadataRepository
    {
        public List<TepCandidateProfileMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepCandidateProfileMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepCandidateProfileMetadata>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepCandidateProfileMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryAssociationMembershipRegistryRepository : ITepAssociationMembershipRegistryRepository
    {
        public List<TepAssociationMembershipRegistry> Items { get; } = [];
        public Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepAssociationMembershipRegistry>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct) { Items.Add(registry); return Task.CompletedTask; }
        public Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryConsentVisibilityPolicyRepository : ITepConsentVisibilityPolicyRepository
    {
        public List<TepConsentVisibilityPolicy> Items { get; } = [];
        public Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepConsentVisibilityPolicy>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct) { Items.Add(policy); return Task.CompletedTask; }
        public Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryVerifiedParticipantAccessRepository : ITepVerifiedParticipantAccessRepository
    {
        public List<TepVerifiedParticipantAccess> Items { get; } = [];
        public Task<IReadOnlyList<TepVerifiedParticipantAccess>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepVerifiedParticipantAccess>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepVerifiedParticipantAccess?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepVerifiedParticipantAccess access, CancellationToken ct) { Items.Add(access); return Task.CompletedTask; }
        public Task UpdateAsync(TepVerifiedParticipantAccess access, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryReviewBoardCaseMetadataRepository : ITepReviewBoardCaseMetadataRepository
    {
        public List<TepReviewBoardCaseMetadata> Items { get; } = [];
        public Task<IReadOnlyList<TepReviewBoardCaseMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepReviewBoardCaseMetadata>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepReviewBoardCaseMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct) { Items.Add(metadata); return Task.CompletedTask; }
        public Task UpdateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryTrustLevelPolicyMetadataRepository : ITepTrustLevelPolicyMetadataRepository
    {
        public List<TepTrustLevelPolicyMetadata> Items { get; } = [];
        public Task<IReadOnlyList<TepTrustLevelPolicyMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepTrustLevelPolicyMetadata>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepTrustLevelPolicyMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct) { Items.Add(metadata); return Task.CompletedTask; }
        public Task UpdateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }
}
