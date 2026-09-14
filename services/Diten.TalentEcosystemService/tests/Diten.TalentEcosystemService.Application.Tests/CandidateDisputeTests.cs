using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class CandidateDisputeTests
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
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("DISPUTE-001");

        var created = await handler.Handle(new CreateCandidateDisputeReadinessCommand(request), CancellationToken.None);
        var list = await new GetCandidateDisputeReadinessListHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(), CancellationToken.None);
        var detail = await new GetCandidateDisputeReadinessByIdHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.Equal("DISPUTE-001", detail.Data!.Code);
        Assert.Equal(request.DisputeReadinessState, detail.Data.DisputeReadinessState);
        Assert.Equal(request.ResponseBoundaryState, detail.Data.ResponseBoundaryState);
        Assert.Equal(request.DisputeIntakeState, detail.Data.DisputeIntakeState);
        Assert.Equal(request.CandidateProfileReference, detail.Data.CandidateProfileReference);
        Assert.Equal(request.DisputeReadinessVersion, detail.Data.DisputeReadinessVersion);
        Assert.Equal(request.DependencyStates.Single().DependencyKey, detail.Data.DependencyStates.Single().DependencyKey);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateCandidateDisputeReadinessCommand(ValidRequest("DISPUTE-TENANT")), CancellationToken.None);
        var query = new GetCandidateDisputeReadinessByIdHandler(repository, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("DISPUTE-DUP");

        var first = await handler.Handle(new CreateCandidateDisputeReadinessCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateCandidateDisputeReadinessCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_deleted_at_and_hides_record()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateCandidateDisputeReadinessCommand(ValidRequest("DISPUTE-ARCH")), CancellationToken.None);
        var archive = new ArchiveCandidateDisputeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repository.Items.Single();
        var list = await repository.ListAsync(TenantA, new[] { Holding }, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepCandidateDisputeReadinessState.Archived, stored.DisputeReadinessState);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Ready_create_fails_closed_when_required_reference_is_missing()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA);

        var result = await handler.Handle(new CreateCandidateDisputeReadinessCommand(ReadyRequest("DISPUTE-MISSING") with
        {
            RehireRecommendationReference = null
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_evaluation_succeeds_with_approved_metadata_preconditions()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateCandidateDisputeReadinessCommand(ReadyRequest("DISPUTE-OK") with
            {
                DisputeReadinessState = TepCandidateDisputeReadinessState.Deferred
            }), CancellationToken.None);
        var evaluate = new EvaluateCandidateDisputeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.ReadinessAllowed);
        Assert.Equal(TepCandidateDisputeReadinessState.Ready, repository.Items.Single().DisputeReadinessState);
    }

    [Fact]
    public async Task Non_readiness_evaluation_without_preconditions_returns_deferred_metadata()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateCandidateDisputeReadinessCommand(ValidRequest("DISPUTE-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateCandidateDisputeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ReadinessAllowed);
        Assert.Equal(TepCandidateDisputeReadinessState.Deferred, repository.Items.Single().DisputeReadinessState);
        Assert.Equal(TepAuditReadinessState.Deferred, repository.Items.Single().AuditReadinessState);
        Assert.Equal("Candidate dispute readiness dependency evaluation deferred.", repository.Items.Single().DeferredReason);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("national_id")]
    [InlineData("dateofbirth")]
    [InlineData("home_address")]
    [InlineData("credential_secret")]
    [InlineData("provider_payload")]
    [InlineData("pii_heavy")]
    [InlineData("dispute_body")]
    [InlineData("free_text_complaint")]
    [InlineData("document_payload")]
    [InlineData("attachment_payload")]
    [InlineData("automated_decision")]
    [InlineData("recommendation_recalculation")]
    [InlineData("marketplace_transaction")]
    [InlineData("notification_delivery")]
    [InlineData("document_repository")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("DISPUTE-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateCandidateDisputeReadinessCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.DoesNotContain(typeof(CandidateDisputeReadinessRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public void Public_contract_does_not_expose_forbidden_workflow_or_sensitive_payload_fields()
    {
        var contractTypes = new[]
        {
            typeof(CandidateDisputeReadinessRequest),
            typeof(CandidateDisputeReadinessDto),
            typeof(CandidateDisputeReadinessListItemDto),
            typeof(TepCandidateDisputeReadinessMetadata)
        };

        var forbiddenProperties = new[]
        {
            "ResponseBody",
            "DisputeBody",
            "DisputeNarrative",
            "ComplaintBody",
            "DocumentBody",
            "DocumentPayload",
            "AttachmentPayload",
            "EvidenceBody",
            "Credential",
            "Token",
            "Secret",
            "Password",
            "RecommendationScore",
            "RecommendationRank",
            "AutomatedDecisionResult",
            "MarketplaceTransaction"
        };

        foreach (var contractType in contractTypes)
        {
            var propertyNames = contractType.GetProperties().Select(property => property.Name).ToHashSet();
            foreach (var forbiddenProperty in forbiddenProperties)
            {
                Assert.DoesNotContain(forbiddenProperty, propertyNames);
            }
        }

        var dependencyCarrierTypes = new[]
        {
            typeof(CandidateDisputeReadinessRequest),
            typeof(CandidateDisputeReadinessDto),
            typeof(TepCandidateDisputeReadinessMetadata)
        };

        foreach (var contractType in dependencyCarrierTypes)
        {
            Assert.Contains("DependencyStates", contractType.GetProperties().Select(property => property.Name));
        }
    }

    [Fact]
    public void Controller_permissions_match_candidate_disputes_namespace()
    {
        Assert.NotNull(typeof(CandidateDisputesController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(CandidateDisputesController.GetAll), CandidateDisputePermissions.Read);
        AssertPermission(nameof(CandidateDisputesController.GetById), CandidateDisputePermissions.Read);
        AssertPermission(nameof(CandidateDisputesController.Create), CandidateDisputePermissions.Manage);
        AssertPermission(nameof(CandidateDisputesController.Update), CandidateDisputePermissions.Manage);
        AssertPermission(nameof(CandidateDisputesController.Archive), CandidateDisputePermissions.Manage);
        AssertPermission(nameof(CandidateDisputesController.Evaluate), CandidateDisputePermissions.Evaluate);
        AssertPermission(nameof(CandidateDisputesController.GetAuditMetadata), CandidateDisputePermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_candidate_dispute_readiness", MongoTepCandidateDisputeReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_candidate_dispute_readiness_tenant_code_active", MongoTepCandidateDisputeReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_candidate_dispute_readiness_tenant_state", MongoTepCandidateDisputeReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Permission_constants_do_not_use_legacy_or_candidate_identifiers()
    {
        foreach (var value in typeof(CandidateDisputePermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null) as string))
        {
            Assert.NotNull(value);
            Assert.StartsWith("tep.candidate-disputes.", value);
            Assert.DoesNotContain("CAND-", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD-", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateCandidateDisputeReadinessCommand(ValidRequest("DISPUTE-LE")), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, repository.Items.Single().LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var repository = new MemoryCandidateDisputeRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateCandidateDisputeReadinessCommand(ValidRequest("DISPUTE-403")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    private static CreateCandidateDisputeReadinessHandler CreateHandler(MemoryCandidateDisputeRepository repository, Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

    private static CreateCandidateDisputeReadinessHandler CreateHandler(
        MemoryCandidateDisputeRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static CandidateDisputeReadinessRequest ValidRequest(string code) =>
        new(
            code,
            "Candidate dispute readiness metadata",
            TepCandidateDisputeReadinessState.Deferred,
            null,
            null,
            null,
            null,
            TepCandidateResponseBoundaryState.Deferred,
            TepDisputeIntakeState.Deferred,
            TepDisputeReviewState.Deferred,
            TepResolutionLifecycleState.Deferred,
            TepContestabilityState.Deferred,
            TepHumanReviewState.Deferred,
            TepConsentRequirementState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepDataScopeState.Deferred,
            TepEvidenceRetentionDecisionState.Deferred,
            TepAuditReadinessState.Deferred,
            TepLocalDeferredPolicyState.Deferred,
            TepLocalDeferredPolicyState.Deferred,
            TepSelfServiceBoundaryState.Deferred,
            TepExternalDependencyState.Deferred,
            TepExternalDependencyState.Deferred,
            TepCandidateDisputeBoundaryState.Deferred,
            TepCandidateDisputeBoundaryState.Deferred,
            [new CandidateDisputeDependencyStateDto("tep.rehire-recommendations", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static CandidateDisputeReadinessRequest ReadyRequest(string code) =>
        ValidRequest(code) with
        {
            DisputeReadinessState = TepCandidateDisputeReadinessState.Ready,
            CandidateProfileReference = Guid.NewGuid(),
            ExitReferenceRecordReference = Guid.NewGuid(),
            ReferenceExchangeReference = Guid.NewGuid(),
            RehireRecommendationReference = Guid.NewGuid(),
            ResponseBoundaryState = TepCandidateResponseBoundaryState.LocalMetadata,
            DisputeIntakeState = TepDisputeIntakeState.LocalMetadata,
            DisputeReviewState = TepDisputeReviewState.LocalMetadata,
            ResolutionLifecycleState = TepResolutionLifecycleState.LocalMetadata,
            ContestabilityState = TepContestabilityState.LocalMetadata,
            HumanReviewState = TepHumanReviewState.LocalMetadata,
            ConsentPreconditionState = TepConsentRequirementState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            DataScopeState = TepDataScopeState.Available,
            EvidenceRetentionState = TepEvidenceRetentionDecisionState.LocalMetadata,
            AuditReadinessState = TepAuditReadinessState.LocalMetadata,
            LegalHoldState = TepLocalDeferredPolicyState.LocalMetadata,
            DeletionPolicyState = TepLocalDeferredPolicyState.LocalMetadata,
            SelfServiceBoundaryState = TepSelfServiceBoundaryState.OutOfScope,
            NotificationDependencyState = TepExternalDependencyState.OutOfScope,
            DocumentDependencyState = TepExternalDependencyState.OutOfScope,
            AutomatedDecisionBoundaryState = TepCandidateDisputeBoundaryState.OutOfScope,
            MarketplaceBoundaryState = TepCandidateDisputeBoundaryState.OutOfScope,
            DependencyStates = AvailableDependencyStates(),
            DeferredReason = null
        };

    private static IReadOnlyList<CandidateDisputeDependencyStateDto> AvailableDependencyStates() =>
    [
        new("tep.association-memberships", TepShellDependencyStatus.Available, null),
        new("tep.consent-visibility-policies", TepShellDependencyStatus.Available, null),
        new("tep.verified-participants", TepShellDependencyStatus.Available, null),
        new("tep.review-board", TepShellDependencyStatus.Available, null),
        new("tep.trust-levels", TepShellDependencyStatus.Available, null),
        new("tep.candidate-profiles", TepShellDependencyStatus.Available, null),
        new("tep.exit-reference-records", TepShellDependencyStatus.Available, null),
        new("tep.reference-exchange", TepShellDependencyStatus.Available, null),
        new("tep.rehire-recommendations", TepShellDependencyStatus.Available, null)
    ];

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(CandidateDisputesController)
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

    private sealed class MemoryCandidateDisputeRepository : ITepCandidateDisputeReadinessMetadataRepository
    {
        public List<TepCandidateDisputeReadinessMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepCandidateDisputeReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepCandidateDisputeReadinessMetadata>>(
                Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<TepCandidateDisputeReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(x =>
                x.TenantId == tenantId
                && x.LegalEntityId == legalEntityId
                && !x.IsDeleted
                && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                && x.Id != excludingId));

        public Task CreateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepCandidateDisputeReadinessMetadata metadata, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
