using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Handlers;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class RehireRecommendationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    [Fact]
    public async Task Create_list_and_get_preserve_metadata_contract()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REHIRE-001");

        var created = await handler.Handle(new CreateRehireRecommendationReadinessCommand(request), CancellationToken.None);
        var list = await new GetRehireRecommendationReadinessListHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(), CancellationToken.None);
        var detail = await new GetRehireRecommendationReadinessByIdHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.Equal("REHIRE-001", detail.Data!.Code);
        Assert.Equal(request.RecommendationReadinessState, detail.Data.RecommendationReadinessState);
        Assert.Equal(request.RecommendationPolicyState, detail.Data.RecommendationPolicyState);
        Assert.Equal(request.RecommendationEvaluationState, detail.Data.RecommendationEvaluationState);
        Assert.Equal(request.ReferenceExchangeReference, detail.Data.ReferenceExchangeReference);
        Assert.Equal(request.RecommendationNetworkVersion, detail.Data.RecommendationNetworkVersion);
        Assert.Equal(request.DependencyStates.Single().DependencyKey, detail.Data.DependencyStates.Single().DependencyKey);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateRehireRecommendationReadinessCommand(ValidRequest("REHIRE-TENANT")), CancellationToken.None);
        var query = new GetRehireRecommendationReadinessByIdHandler(repository, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REHIRE-DUP");

        var first = await handler.Handle(new CreateRehireRecommendationReadinessCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateRehireRecommendationReadinessCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_deleted_at_and_hides_record()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateRehireRecommendationReadinessCommand(ValidRequest("REHIRE-ARCH")), CancellationToken.None);
        var archive = new ArchiveRehireRecommendationReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repository.Items.Single();
        var list = await repository.ListAsync(TenantA, new[] { Holding }, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepRehireRecommendationReadinessState.Archived, stored.RecommendationReadinessState);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Ready_create_fails_closed_when_required_reference_is_missing()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA);

        var result = await handler.Handle(new CreateRehireRecommendationReadinessCommand(ReadyRequest("REHIRE-MISSING") with
        {
            ReferenceExchangeReference = null
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_evaluation_succeeds_with_approved_metadata_preconditions()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateRehireRecommendationReadinessCommand(ReadyRequest("REHIRE-OK") with
            {
                RecommendationReadinessState = TepRehireRecommendationReadinessState.Deferred
            }), CancellationToken.None);
        var evaluate = new EvaluateRehireRecommendationReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.ReadinessAllowed);
        Assert.Equal(TepRehireRecommendationReadinessState.Ready, repository.Items.Single().RecommendationReadinessState);
        Assert.Equal(TepRehireRecommendationEvaluationState.Ready, repository.Items.Single().RecommendationEvaluationState);
    }

    [Fact]
    public async Task Non_readiness_evaluation_without_preconditions_returns_deferred_metadata()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateRehireRecommendationReadinessCommand(ValidRequest("REHIRE-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateRehireRecommendationReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ReadinessAllowed);
        Assert.Equal(TepRehireRecommendationReadinessState.Deferred, repository.Items.Single().RecommendationReadinessState);
        Assert.Equal(TepRehireRecommendationEvaluationState.Deferred, repository.Items.Single().RecommendationEvaluationState);
        Assert.Equal(TepAuditReadinessState.Deferred, repository.Items.Single().AuditReadinessState);
        Assert.Equal("Rehire recommendation readiness dependency evaluation deferred.", repository.Items.Single().DeferredReason);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("national_id")]
    [InlineData("dateofbirth")]
    [InlineData("home_address")]
    [InlineData("credential_secret")]
    [InlineData("provider_payload")]
    [InlineData("pii_heavy")]
    [InlineData("recommendation_score")]
    [InlineData("ranking_output")]
    [InlineData("model_output")]
    [InlineData("automated_decision")]
    [InlineData("dispute_workflow")]
    [InlineData("notification_delivery")]
    [InlineData("document_repository")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REHIRE-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateRehireRecommendationReadinessCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.DoesNotContain(typeof(RehireRecommendationReadinessRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public void Public_contract_does_not_expose_scoring_or_decision_outputs()
    {
        var contractTypes = new[]
        {
            typeof(RehireRecommendationReadinessRequest),
            typeof(RehireRecommendationReadinessDto),
            typeof(RehireRecommendationReadinessListItemDto),
            typeof(TepRehireRecommendationReadinessMetadata)
        };

        foreach (var contractType in contractTypes)
        {
            var propertyNames = contractType.GetProperties().Select(property => property.Name).ToHashSet();
            Assert.DoesNotContain("Score", propertyNames);
            Assert.DoesNotContain("Rank", propertyNames);
            Assert.DoesNotContain("ModelOutput", propertyNames);
            Assert.DoesNotContain("EligibilityLabel", propertyNames);
            Assert.DoesNotContain("AutomatedDecisionResult", propertyNames);
        }

        var dependencyCarrierTypes = new[]
        {
            typeof(RehireRecommendationReadinessRequest),
            typeof(RehireRecommendationReadinessDto),
            typeof(TepRehireRecommendationReadinessMetadata)
        };

        foreach (var contractType in dependencyCarrierTypes)
        {
            Assert.Contains("DependencyStates", contractType.GetProperties().Select(property => property.Name));
        }
    }

    [Fact]
    public void Controller_permissions_match_rehire_recommendations_namespace()
    {
        Assert.NotNull(typeof(RehireRecommendationsController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(RehireRecommendationsController.GetAll), RehireRecommendationPermissions.Read);
        AssertPermission(nameof(RehireRecommendationsController.GetById), RehireRecommendationPermissions.Read);
        AssertPermission(nameof(RehireRecommendationsController.Create), RehireRecommendationPermissions.Manage);
        AssertPermission(nameof(RehireRecommendationsController.Update), RehireRecommendationPermissions.Manage);
        AssertPermission(nameof(RehireRecommendationsController.Archive), RehireRecommendationPermissions.Manage);
        AssertPermission(nameof(RehireRecommendationsController.Evaluate), RehireRecommendationPermissions.Evaluate);
        AssertPermission(nameof(RehireRecommendationsController.GetAuditMetadata), RehireRecommendationPermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_rehire_recommendation_readiness", MongoTepRehireRecommendationReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_rehire_recommendation_readiness_tenant_code_active", MongoTepRehireRecommendationReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_rehire_recommendation_readiness_tenant_state", MongoTepRehireRecommendationReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Permission_constants_do_not_use_legacy_or_candidate_identifiers()
    {
        foreach (var value in typeof(RehireRecommendationPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null) as string))
        {
            Assert.NotNull(value);
            Assert.StartsWith("tep.rehire-recommendations.", value);
            Assert.DoesNotContain("CAND", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateRehireRecommendationReadinessCommand(ValidRequest("REHIRE-LE")), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, repository.Items.Single().LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var repository = new MemoryRehireRecommendationRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateRehireRecommendationReadinessCommand(ValidRequest("REHIRE-403")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    private static CreateRehireRecommendationReadinessHandler CreateHandler(MemoryRehireRecommendationRepository repository, Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

    private static CreateRehireRecommendationReadinessHandler CreateHandler(
        MemoryRehireRecommendationRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static RehireRecommendationReadinessRequest ValidRequest(string code) =>
        new(
            code,
            "Rehire readiness metadata",
            TepRehireRecommendationReadinessState.Deferred,
            TepRehireRecommendationPolicyState.Deferred,
            TepRehireRecommendationEvaluationState.NotEvaluated,
            TepRecommendationEligibilityPreconditionState.Deferred,
            TepConsentRequirementState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepDataScopeState.Deferred,
            TepDataMinimizationState.Deferred,
            TepRecommendationExplainabilityState.Deferred,
            TepHumanReviewState.Deferred,
            TepContestabilityState.Deferred,
            TepReviewDisputeBoundaryState.Deferred,
            TepAbuseControlState.Deferred,
            TepMisuseDetectionState.Deferred,
            TepThrottlingPolicyState.Deferred,
            TepEscalationState.Deferred,
            TepEvidenceRetentionDecisionState.Deferred,
            TepAuditReadinessState.Deferred,
            TepLocalDeferredPolicyState.Deferred,
            TepLocalDeferredPolicyState.Deferred,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [new RehireRecommendationDependencyStateDto("tep.reference-exchange", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static RehireRecommendationReadinessRequest ReadyRequest(string code) =>
        ValidRequest(code) with
        {
            RecommendationReadinessState = TepRehireRecommendationReadinessState.Ready,
            RecommendationPolicyState = TepRehireRecommendationPolicyState.LocalMetadata,
            RecommendationEvaluationState = TepRehireRecommendationEvaluationState.Ready,
            EligibilityPreconditionState = TepRecommendationEligibilityPreconditionState.LocalMetadata,
            ConsentPreconditionState = TepConsentRequirementState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            DataScopeState = TepDataScopeState.Available,
            MinimizationState = TepDataMinimizationState.Approved,
            ExplainabilityState = TepRecommendationExplainabilityState.LocalMetadata,
            HumanReviewState = TepHumanReviewState.LocalMetadata,
            ContestabilityState = TepContestabilityState.LocalMetadata,
            CandidateResponseBoundaryState = TepReviewDisputeBoundaryState.PreconditionSatisfied,
            AbuseControlState = TepAbuseControlState.LocalMetadata,
            MisuseDetectionState = TepMisuseDetectionState.LocalMetadata,
            ThrottlingState = TepThrottlingPolicyState.LocalMetadata,
            EscalationState = TepEscalationState.LocalMetadata,
            EvidenceRetentionState = TepEvidenceRetentionDecisionState.LocalMetadata,
            AuditReadinessState = TepAuditReadinessState.LocalMetadata,
            LegalHoldState = TepLocalDeferredPolicyState.LocalMetadata,
            DeletionPolicyState = TepLocalDeferredPolicyState.LocalMetadata,
            ReferenceExchangeReference = Guid.NewGuid(),
            ExitReferenceRecordReference = Guid.NewGuid(),
            CandidateProfileReference = Guid.NewGuid(),
            VerifiedParticipantReference = Guid.NewGuid(),
            AssociationMembershipReference = Guid.NewGuid(),
            ConsentVisibilityPolicyReference = Guid.NewGuid(),
            ReviewBoardCaseReference = Guid.NewGuid(),
            TrustLevelPolicyReference = Guid.NewGuid(),
            DependencyStates = AvailableDependencyStates(),
            DeferredReason = null
        };

    private static IReadOnlyList<RehireRecommendationDependencyStateDto> AvailableDependencyStates() =>
    [
        new("tep.association-memberships", TepShellDependencyStatus.Available, null),
        new("tep.consent-visibility-policies", TepShellDependencyStatus.Available, null),
        new("tep.verified-participants", TepShellDependencyStatus.Available, null),
        new("tep.review-board", TepShellDependencyStatus.Available, null),
        new("tep.trust-levels", TepShellDependencyStatus.Available, null),
        new("tep.candidate-profiles", TepShellDependencyStatus.Available, null),
        new("tep.exit-reference-records", TepShellDependencyStatus.Available, null),
        new("tep.reference-exchange", TepShellDependencyStatus.Available, null)
    ];

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(RehireRecommendationsController)
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

    private sealed class MemoryRehireRecommendationRepository : ITepRehireRecommendationReadinessMetadataRepository
    {
        public List<TepRehireRecommendationReadinessMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepRehireRecommendationReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepRehireRecommendationReadinessMetadata>>(
                Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<TepRehireRecommendationReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(x =>
                x.TenantId == tenantId
                && x.LegalEntityId == legalEntityId
                && !x.IsDeleted
                && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                && x.Id != excludingId));

        public Task CreateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
