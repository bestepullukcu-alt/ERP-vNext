using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class ReviewBoardTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var handler = CreateHandler(reviewCases, TenantA);
        var request = ValidRequest("REV-A");

        var first = await handler.Handle(new CreateReviewBoardCaseCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateReviewBoardCaseCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var created = await CreateHandler(reviewCases, TenantA)
            .Handle(new CreateReviewBoardCaseCommand(ValidRequest("REV-TENANT")), CancellationToken.None);
        var query = new GetReviewBoardCaseByIdHandler(reviewCases, new FixedTenantContext(TenantB));

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var created = await CreateHandler(reviewCases, TenantA)
            .Handle(new CreateReviewBoardCaseCommand(ValidRequest("REV-ARCH")), CancellationToken.None);
        var archive = new ArchiveReviewBoardCaseHandler(reviewCases, new FixedTenantContext(TenantA));

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = reviewCases.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepReviewBoardCaseState.Archived, stored.ReviewBoardCaseState);
        Assert.Equal(TepReviewDecisionState.Archived, stored.ReviewDecisionState);
    }

    [Fact]
    public async Task Review_decision_creation_fails_closed_without_preconditions()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var handler = CreateHandler(reviewCases, TenantA);

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(DecisionRequest("REV-BLOCKED", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Review_decision_creation_requires_same_tenant_association_dependency()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantB, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var handler = new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(DecisionRequest("REV-CROSS-ASSOC", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Review_decision_creation_requires_same_tenant_policy_dependency()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantB);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var handler = new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(DecisionRequest("REV-CROSS-POLICY", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Review_decision_creation_requires_same_tenant_verified_access_dependency()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantB, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var handler = new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(DecisionRequest("REV-CROSS-VER", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Review_decision_creation_succeeds_with_same_tenant_approved_preconditions()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var handler = new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(DecisionRequest("REV-OK", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Evaluation_without_available_preconditions_returns_explicit_deferred_metadata()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var created = await CreateHandler(reviewCases, TenantA)
            .Handle(new CreateReviewBoardCaseCommand(ValidRequest("REV-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateReviewBoardCaseHandler(
            reviewCases,
            new InMemoryAssociationMembershipRegistryRepository(),
            new InMemoryConsentVisibilityPolicyRepository(),
            new InMemoryVerifiedParticipantAccessRepository(),
            new FixedTenantContext(TenantA));

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ReviewAllowed);
        Assert.Equal(TepReviewBoardCaseState.Deferred, reviewCases.Items.Single().ReviewBoardCaseState);
        Assert.Equal(TepReviewDecisionState.Deferred, reviewCases.Items.Single().ReviewDecisionState);
        Assert.Equal("Review-board dependency evaluation deferred.", reviewCases.Items.Single().DeferredReason);
    }

    [Fact]
    public async Task Review_request_fails_closed_when_reviewer_eligibility_unavailable()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var created = await new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA))
            .Handle(new CreateReviewBoardCaseCommand(ReadyDeferredRequest("REV-ELIG", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);
        var review = new ReviewReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await review.Handle(new(created.Data, new(TepReviewDecisionState.Approved)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Review_path_records_decision_with_same_tenant_preconditions_and_sod()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var associations = new InMemoryAssociationMembershipRegistryRepository();
        var policies = new InMemoryConsentVisibilityPolicyRepository();
        var verified = new InMemoryVerifiedParticipantAccessRepository();
        var policy = ApprovedPolicy(TenantA);
        var association = ApprovedAssociation(TenantA, policy.Id);
        var verifiedAccess = ApprovedVerifiedAccess(TenantA, association.Id, policy.Id);
        await policies.CreateAsync(policy, CancellationToken.None);
        await associations.CreateAsync(association, CancellationToken.None);
        await verified.CreateAsync(verifiedAccess, CancellationToken.None);
        var created = await new CreateReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA))
            .Handle(new CreateReviewBoardCaseCommand(DecisionReadyRequest("REV-REVIEW", association.Id, policy.Id, verifiedAccess.Id)), CancellationToken.None);
        var review = new ReviewReviewBoardCaseHandler(reviewCases, associations, policies, verified, new FixedTenantContext(TenantA));

        var result = await review.Handle(new(created.Data, new(TepReviewDecisionState.Approved)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepReviewBoardCaseState.DecisionRecorded, reviewCases.Items.Single().ReviewBoardCaseState);
        Assert.Equal(TepReviewDecisionState.Approved, reviewCases.Items.Single().ReviewDecisionState);
    }

    [Fact]
    public async Task Audit_metadata_is_local_deferred_only()
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var created = await CreateHandler(reviewCases, TenantA)
            .Handle(new CreateReviewBoardCaseCommand(ValidRequest("REV-AUDIT")), CancellationToken.None);
        var audit = new GetReviewBoardCaseAuditMetadataHandler(reviewCases, new FixedTenantContext(TenantA));

        var result = await audit.Handle(new(created.Data), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepAuditEvidenceState.Deferred, result.Data!.AuditEvidenceState);
        Assert.Equal(TepRetentionState.Deferred, result.Data.RetentionState);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("talent_identity")]
    [InlineData("reference_exchange")]
    [InlineData("dispute_workflow")]
    [InlineData("pii_heavy")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var reviewCases = new InMemoryReviewBoardCaseMetadataRepository();
        var handler = CreateHandler(reviewCases, TenantA);
        var request = ValidRequest("REV-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateReviewBoardCaseCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_review_board_namespace()
    {
        Assert.NotNull(typeof(ReviewBoardController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(ReviewBoardController.GetAll), ReviewBoardPermissions.Read);
        AssertPermission(nameof(ReviewBoardController.GetById), ReviewBoardPermissions.Read);
        AssertPermission(nameof(ReviewBoardController.Create), ReviewBoardPermissions.Manage);
        AssertPermission(nameof(ReviewBoardController.Update), ReviewBoardPermissions.Manage);
        AssertPermission(nameof(ReviewBoardController.Archive), ReviewBoardPermissions.Manage);
        AssertPermission(nameof(ReviewBoardController.Evaluate), ReviewBoardPermissions.Review);
        AssertPermission(nameof(ReviewBoardController.Review), ReviewBoardPermissions.Review);
        AssertPermission(nameof(ReviewBoardController.GetAuditMetadata), ReviewBoardPermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_review_board_cases", MongoTepReviewBoardCaseMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_review_board_cases_tenant_code_active", MongoTepReviewBoardCaseMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_review_board_cases_tenant_state", MongoTepReviewBoardCaseMetadataRepository.TenantStateIndexName);
    }

    private static CreateReviewBoardCaseHandler CreateHandler(InMemoryReviewBoardCaseMetadataRepository repository, Guid tenantId) =>
        new(
            repository,
            new InMemoryAssociationMembershipRegistryRepository(),
            new InMemoryConsentVisibilityPolicyRepository(),
            new InMemoryVerifiedParticipantAccessRepository(),
            new FixedTenantContext(tenantId));

    private static ReviewBoardCaseRequest ValidRequest(string code) =>
        new(
            code,
            "Governance review case",
            TepReviewBoardCaseState.Deferred,
            TepReviewDecisionState.Deferred,
            null,
            null,
            null,
            TepReviewerEligibilityState.Deferred,
            TepSegregationOfDutiesState.Deferred,
            TepLegalSecurityDecisionState.Deferred,
            TepExternalReviewBoardState.Deferred,
            TepAuditEvidenceState.Deferred,
            TepRetentionState.Deferred,
            [new ReviewBoardDependencyStateDto("tep.association-memberships", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static ReviewBoardCaseRequest ReadyDeferredRequest(string code, Guid associationId, Guid policyId, Guid verifiedAccessId) =>
        ValidRequest(code) with
        {
            AssociationMembershipRegistryId = associationId,
            ConsentVisibilityPolicyId = policyId,
            VerifiedParticipantAccessId = verifiedAccessId,
            ReviewerEligibilityState = TepReviewerEligibilityState.Deferred,
            SegregationOfDutiesState = TepSegregationOfDutiesState.Deferred,
            LegalSecurityDecisionState = TepLegalSecurityDecisionState.Deferred,
            ExternalReviewBoardState = TepExternalReviewBoardState.NotRequired
        };

    private static ReviewBoardCaseRequest DecisionReadyRequest(string code, Guid associationId, Guid policyId, Guid verifiedAccessId) =>
        ReadyDeferredRequest(code, associationId, policyId, verifiedAccessId) with
        {
            ReviewerEligibilityState = TepReviewerEligibilityState.Eligible,
            SegregationOfDutiesState = TepSegregationOfDutiesState.Passed,
            LegalSecurityDecisionState = TepLegalSecurityDecisionState.Approved,
            DeferredReason = null
        };

    private static ReviewBoardCaseRequest DecisionRequest(string code, Guid associationId, Guid policyId, Guid verifiedAccessId) =>
        DecisionReadyRequest(code, associationId, policyId, verifiedAccessId) with
        {
            ReviewBoardCaseState = TepReviewBoardCaseState.DecisionRecorded,
            ReviewDecisionState = TepReviewDecisionState.Approved
        };

    private static TepAssociationMembershipRegistry ApprovedAssociation(Guid tenantId, Guid policyId) =>
        new()
        {
            TenantId = tenantId,
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

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(ReviewBoardController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class InMemoryReviewBoardCaseMetadataRepository : ITepReviewBoardCaseMetadataRepository
    {
        public List<TepReviewBoardCaseMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepReviewBoardCaseMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepReviewBoardCaseMetadata>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepReviewBoardCaseMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
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

    private sealed class InMemoryAssociationMembershipRegistryRepository : ITepAssociationMembershipRegistryRepository
    {
        public List<TepAssociationMembershipRegistry> Items { get; } = [];

        public Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepAssociationMembershipRegistry>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
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

        public Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepConsentVisibilityPolicy>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
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

        public Task<IReadOnlyList<TepVerifiedParticipantAccess>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepVerifiedParticipantAccess>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepVerifiedParticipantAccess?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
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
}
