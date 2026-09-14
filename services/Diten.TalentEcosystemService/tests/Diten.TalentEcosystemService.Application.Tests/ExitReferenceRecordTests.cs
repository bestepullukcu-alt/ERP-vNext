using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class ExitReferenceRecordTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_list_and_get_preserve_metadata_contract()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA);
        var request = ValidRequest("EXIT-REF-001");

        var created = await handler.Handle(new CreateExitReferenceRecordCommand(request), CancellationToken.None);
        var list = await new GetExitReferenceRecordListHandler(repos.ExitReferences, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(), CancellationToken.None);
        var detail = await new GetExitReferenceRecordByIdHandler(repos.ExitReferences, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.Equal("EXIT-REF-001", detail.Data!.Code);
        Assert.Equal(request.CandidateProfileReference, detail.Data.CandidateProfileReference);
        Assert.Equal(request.OffboardingCaseReference, detail.Data.OffboardingCaseReference);
        Assert.Equal(request.ReferenceRecordVersion, detail.Data.ReferenceRecordVersion);
        Assert.Equal(request.DependencyStates.Single().DependencyKey, detail.Data.DependencyStates.Single().DependencyKey);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateExitReferenceRecordCommand(ValidRequest("EXIT-TENANT")), CancellationToken.None);
        var query = new GetExitReferenceRecordByIdHandler(repos.ExitReferences, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA);
        var request = ValidRequest("EXIT-DUP");

        var first = await handler.Handle(new CreateExitReferenceRecordCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateExitReferenceRecordCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_deleted_at_and_hides_record()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateExitReferenceRecordCommand(ValidRequest("EXIT-ARCH")), CancellationToken.None);
        var archive = new ArchiveExitReferenceRecordHandler(repos.ExitReferences, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repos.ExitReferences.Items.Single();
        var list = await repos.ExitReferences.ListAsync(TenantA, new[] { Holding }, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepExitReferenceRecordState.Archived, stored.ReferenceRecordState);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Activating_create_fails_closed_when_candidate_profile_dependency_is_missing()
    {
        var repos = new Repositories();
        var deps = await SeedApprovedDependenciesAsync(repos, TenantA);
        var handler = CreateHandler(repos, TenantA);

        var result = await handler.Handle(new CreateExitReferenceRecordCommand(ActiveRequest("EXIT-MISSING", deps) with
        {
            CandidateProfileReference = Guid.NewGuid()
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Activation_evaluation_succeeds_with_same_tenant_approved_preconditions()
    {
        var repos = new Repositories();
        var deps = await SeedApprovedDependenciesAsync(repos, TenantA);
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateExitReferenceRecordCommand(ActiveRequest("EXIT-OK", deps) with
            {
                ReferenceRecordState = TepExitReferenceRecordState.Deferred
            }), CancellationToken.None);
        var evaluate = EvaluateHandler(repos, TenantA);

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.ActivationAllowed);
        Assert.Equal(TepExitReferenceRecordState.Active, repos.ExitReferences.Items.Single().ReferenceRecordState);
        Assert.Equal(TepReferenceSharingState.LocalMetadata, repos.ExitReferences.Items.Single().ReferenceSharingState);
    }

    [Fact]
    public async Task Non_activating_evaluation_without_dependencies_returns_deferred_metadata()
    {
        var repos = new Repositories();
        var created = await CreateHandler(repos, TenantA)
            .Handle(new CreateExitReferenceRecordCommand(ValidRequest("EXIT-DEFER")), CancellationToken.None);
        var evaluate = EvaluateHandler(repos, TenantA);

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ActivationAllowed);
        Assert.Equal(TepExitReferenceRecordState.Deferred, repos.ExitReferences.Items.Single().ReferenceRecordState);
        Assert.Equal(TepEvidenceRetentionDecisionState.Deferred, repos.ExitReferences.Items.Single().EvidenceRetentionState);
        Assert.Equal("Exit reference record dependency evaluation deferred.", repos.ExitReferences.Items.Single().DeferredReason);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("national_id")]
    [InlineData("dateofbirth")]
    [InlineData("home_address")]
    [InlineData("credential_secret")]
    [InlineData("provider_payload")]
    [InlineData("pii_heavy")]
    [InlineData("reference_exchange")]
    [InlineData("dispute_workflow")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA);
        var request = ValidRequest("EXIT-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateExitReferenceRecordCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.DoesNotContain(typeof(ExitReferenceRecordRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public void Dependency_precondition_state_is_carried_only_by_dependency_states_contract()
    {
        var forbiddenProperties = new[]
        {
            "Association" + "Validation" + "State",
            "ConsentVisibility" + "Validation" + "State",
            "VerifiedAccess" + "Validation" + "State",
            "ReviewBoard" + "Validation" + "State",
            "TrustLevel" + "Validation" + "State",
            "CandidateProfile" + "Validation" + "State"
        };
        var contractTypes = new[]
        {
            typeof(ExitReferenceRecordRequest),
            typeof(ExitReferenceRecordDto),
            typeof(TepExitReferenceRecordMetadata)
        };

        foreach (var contractType in contractTypes)
        {
            var propertyNames = contractType.GetProperties().Select(property => property.Name).ToHashSet();
            Assert.Contains("DependencyStates", propertyNames);

            foreach (var propertyName in forbiddenProperties)
            {
                Assert.DoesNotContain(propertyName, propertyNames);
            }
        }
    }

    [Fact]
    public void Controller_permissions_match_exit_reference_namespace()
    {
        Assert.NotNull(typeof(ExitReferenceRecordsController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(ExitReferenceRecordsController.GetAll), ExitReferenceRecordPermissions.Read);
        AssertPermission(nameof(ExitReferenceRecordsController.GetById), ExitReferenceRecordPermissions.Read);
        AssertPermission(nameof(ExitReferenceRecordsController.Create), ExitReferenceRecordPermissions.Manage);
        AssertPermission(nameof(ExitReferenceRecordsController.Update), ExitReferenceRecordPermissions.Manage);
        AssertPermission(nameof(ExitReferenceRecordsController.Archive), ExitReferenceRecordPermissions.Manage);
        AssertPermission(nameof(ExitReferenceRecordsController.Evaluate), ExitReferenceRecordPermissions.Evaluate);
        AssertPermission(nameof(ExitReferenceRecordsController.GetAuditMetadata), ExitReferenceRecordPermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_exit_reference_records", MongoTepExitReferenceRecordMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_exit_reference_records_tenant_code_active", MongoTepExitReferenceRecordMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_exit_reference_records_tenant_state", MongoTepExitReferenceRecordMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Permission_constants_do_not_use_legacy_or_candidate_identifiers()
    {
        foreach (var value in typeof(ExitReferenceRecordPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null) as string))
        {
            Assert.NotNull(value);
            Assert.StartsWith("tep.exit-reference-records.", value);
            Assert.DoesNotContain("CAND", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Create_stamps_the_selected_legal_entity()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateExitReferenceRecordCommand(ValidRequest("EXIT-LE")), CancellationToken.None);
        var stored = repos.ExitReferences.Items.Single();

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task Create_without_a_permitted_legal_entity_is_forbidden()
    {
        var repos = new Repositories();
        var handler = CreateHandler(repos, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var result = await handler.Handle(new CreateExitReferenceRecordCommand(ValidRequest("EXIT-403")), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(repos.ExitReferences.Items);
    }

    private static CreateExitReferenceRecordHandler CreateHandler(Repositories repos, Guid tenantId, ILegalEntityContext? legalEntityContext = null) =>
        new(
            repos.ExitReferences,
            repos.Associations,
            repos.Policies,
            repos.Verified,
            repos.Reviews,
            repos.Trust,
            repos.Candidates,
            new FixedTenantContext(tenantId),
            legalEntityContext ?? PilotLegalEntityContext());

    private static EvaluateExitReferenceRecordHandler EvaluateHandler(Repositories repos, Guid tenantId) =>
        new(
            repos.ExitReferences,
            repos.Associations,
            repos.Policies,
            repos.Verified,
            repos.Reviews,
            repos.Trust,
            repos.Candidates,
            new FixedTenantContext(tenantId),
            PilotLegalEntityContext());

    private static ExitReferenceRecordRequest ValidRequest(string code) =>
        new(
            code,
            "Exit reference metadata",
            null,
            "offboarding-case-ref",
            null,
            null,
            null,
            null,
            null,
            TepExitReferenceRecordState.Deferred,
            TepReferenceSharingState.Deferred,
            TepConsentRequirementState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepDataScopeState.Deferred,
            TepEvidenceRetentionDecisionState.Deferred,
            TepReviewDisputeBoundaryState.Deferred,
            [new ExitReferenceDependencyStateDto("tep.candidate-profiles", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static ExitReferenceRecordRequest ActiveRequest(string code, DependencyIds deps) =>
        ValidRequest(code) with
        {
            CandidateProfileReference = deps.CandidateId,
            OffboardingCaseReference = "offboarding-case-ref",
            VerifiedParticipantReference = deps.VerifiedId,
            AssociationMembershipReference = deps.AssociationId,
            ConsentVisibilityPolicyReference = deps.PolicyId,
            ReviewBoardCaseReference = deps.ReviewId,
            TrustLevelPolicyReference = deps.TrustId,
            ReferenceRecordState = TepExitReferenceRecordState.Active,
            ReferenceSharingState = TepReferenceSharingState.LocalMetadata,
            ConsentPreconditionState = TepConsentRequirementState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            DataScopeState = TepDataScopeState.Available,
            EvidenceRetentionState = TepEvidenceRetentionDecisionState.LocalMetadata,
            ReviewDisputeBoundaryState = TepReviewDisputeBoundaryState.PreconditionSatisfied,
            DependencyStates = AvailableDependencyStates(),
            DeferredReason = null
        };

    private static async Task<DependencyIds> SeedApprovedDependenciesAsync(Repositories repos, Guid tenantId)
    {
        var policy = ApprovedPolicy(tenantId);
        var association = ApprovedAssociation(tenantId, policy.Id);
        var verified = ApprovedVerifiedAccess(tenantId, association.Id, policy.Id);
        var review = ApprovedReviewCase(tenantId, association.Id, policy.Id, verified.Id);
        var trust = ApprovedTrustLevel(tenantId, association.Id, policy.Id, verified.Id, review.Id);
        var candidate = ApprovedCandidateProfile(tenantId, association.Id, policy.Id, verified.Id, review.Id, trust.Id);
        await repos.Policies.CreateAsync(policy, CancellationToken.None);
        await repos.Associations.CreateAsync(association, CancellationToken.None);
        await repos.Verified.CreateAsync(verified, CancellationToken.None);
        await repos.Reviews.CreateAsync(review, CancellationToken.None);
        await repos.Trust.CreateAsync(trust, CancellationToken.None);
        await repos.Candidates.CreateAsync(candidate, CancellationToken.None);
        return new(association.Id, policy.Id, verified.Id, review.Id, trust.Id, candidate.Id);
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
            SignatureSubstrateState = TepSignatureSubstrateState.Available,
            LegalSecurityTrustModelState = TepTrustLegalSecurityState.Approved,
            AuditEvidenceState = TepAuditEvidenceState.Deferred,
            RetentionState = TepRetentionState.Deferred,
            SourceContractVersion = "v1",
            TrustPolicyVersion = 1
        };

    private static TepCandidateProfileMetadata ApprovedCandidateProfile(
        Guid tenantId,
        Guid associationId,
        Guid policyId,
        Guid verifiedId,
        Guid reviewId,
        Guid trustId) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = $"PROF-{tenantId.ToString()[..8]}",
            DisplayName = "Candidate profile",
            CandidateReference = "candidate-ref",
            TalentProfileReference = "profile-ref",
            AssociationMembershipId = associationId,
            ConsentVisibilityPolicyId = policyId,
            VerifiedParticipantId = verifiedId,
            ReviewBoardCaseId = reviewId,
            TrustLevelPolicyId = trustId,
            HcmFoundationReference = "hcm-foundation-ref",
            SkillSummaryMetadata = "skill summary metadata",
            CredentialSummaryMetadata = "credential summary metadata",
            ExperienceSummaryMetadata = "experience summary metadata",
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
            SourceContractVersion = "v1",
            CandidateVersion = 1,
            LocalAuditEvidenceRetentionState = TepLocalAuditEvidenceRetentionState.LocalMetadata
        };

    private static IReadOnlyList<ExitReferenceDependencyStateDto> AvailableDependencyStates() =>
    [
        new("tep.association-memberships", TepShellDependencyStatus.Available, null),
        new("tep.consent-visibility-policies", TepShellDependencyStatus.Available, null),
        new("tep.verified-participants", TepShellDependencyStatus.Available, null),
        new("tep.review-board", TepShellDependencyStatus.Available, null),
        new("tep.trust-levels", TepShellDependencyStatus.Available, null),
        new("tep.candidate-profiles", TepShellDependencyStatus.Available, null)
    ];

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(ExitReferenceRecordsController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private sealed record DependencyIds(Guid AssociationId, Guid PolicyId, Guid VerifiedId, Guid ReviewId, Guid TrustId, Guid CandidateId);

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
        public InMemoryExitReferenceRecordMetadataRepository ExitReferences { get; } = new();
        public InMemoryAssociationMembershipRegistryRepository Associations { get; } = new();
        public InMemoryConsentVisibilityPolicyRepository Policies { get; } = new();
        public InMemoryVerifiedParticipantAccessRepository Verified { get; } = new();
        public InMemoryReviewBoardCaseMetadataRepository Reviews { get; } = new();
        public InMemoryTrustLevelPolicyMetadataRepository Trust { get; } = new();
        public InMemoryCandidateProfileMetadataRepository Candidates { get; } = new();
    }

    private sealed class InMemoryExitReferenceRecordMetadataRepository : ITepExitReferenceRecordMetadataRepository
    {
        public List<TepExitReferenceRecordMetadata> Items { get; } = [];
        public Task<IReadOnlyList<TepExitReferenceRecordMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepExitReferenceRecordMetadata>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());
        public Task<TepExitReferenceRecordMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) && x.Id != excludingId));
        public Task CreateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct) { Items.Add(metadata); return Task.CompletedTask; }
        public Task UpdateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct) => Task.CompletedTask;
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

    private sealed class InMemoryCandidateProfileMetadataRepository : ITepCandidateProfileMetadataRepository
    {
        public List<TepCandidateProfileMetadata> Items { get; } = [];
        public Task<IReadOnlyList<TepCandidateProfileMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<TepCandidateProfileMetadata>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).ToList());
        public Task<TepCandidateProfileMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));
        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && !x.IsDeleted && x.Code == code && x.Id != excludingId));
        public Task CreateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct) { Items.Add(metadata); return Task.CompletedTask; }
        public Task UpdateAsync(TepCandidateProfileMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }
}
