using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Handlers;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class ReferenceExchangeTests
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
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REF-EX-001");

        var created = await handler.Handle(new CreateReferenceExchangeReadinessCommand(request), CancellationToken.None);
        var list = await new GetReferenceExchangeReadinessListHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(), CancellationToken.None);
        var detail = await new GetReferenceExchangeReadinessByIdHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext())
            .Handle(new(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.Equal("REF-EX-001", detail.Data!.Code);
        Assert.Equal(request.ExchangeReadinessState, detail.Data.ExchangeReadinessState);
        Assert.Equal(request.AssociationMembershipReference, detail.Data.AssociationMembershipReference);
        Assert.Equal(request.ReferenceExchangeVersion, detail.Data.ReferenceExchangeVersion);
        Assert.Equal(request.DependencyStates.Single().DependencyKey, detail.Data.DependencyStates.Single().DependencyKey);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateReferenceExchangeReadinessCommand(ValidRequest("REF-TENANT")), CancellationToken.None);
        var query = new GetReferenceExchangeReadinessByIdHandler(repository, new FixedTenantContext(TenantB), PilotLegalEntityContext());

        var result = await query.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REF-DUP");

        var first = await handler.Handle(new CreateReferenceExchangeReadinessCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateReferenceExchangeReadinessCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_deleted_at_and_hides_record()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateReferenceExchangeReadinessCommand(ValidRequest("REF-ARCH")), CancellationToken.None);
        var archive = new ArchiveReferenceExchangeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repository.Items.Single();
        var list = await repository.ListAsync(TenantA, new[] { Holding }, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepReferenceExchangeReadinessState.Archived, stored.ExchangeReadinessState);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Ready_create_fails_closed_when_required_reference_is_missing()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA);

        var result = await handler.Handle(new CreateReferenceExchangeReadinessCommand(ReadyRequest("REF-MISSING") with
        {
            ExitReferenceRecordReference = null
        }), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_evaluation_succeeds_with_approved_metadata_preconditions()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateReferenceExchangeReadinessCommand(ReadyRequest("REF-OK") with
            {
                ExchangeReadinessState = TepReferenceExchangeReadinessState.Deferred
            }), CancellationToken.None);
        var evaluate = new EvaluateReferenceExchangeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.ReadinessAllowed);
        Assert.Equal(TepReferenceExchangeReadinessState.Ready, repository.Items.Single().ExchangeReadinessState);
        Assert.Equal(TepReferenceExchangeAvailabilityState.LocalMetadata, repository.Items.Single().ExchangeAvailabilityState);
    }

    [Fact]
    public async Task Non_readiness_evaluation_without_preconditions_returns_deferred_metadata()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var created = await CreateHandler(repository, TenantA)
            .Handle(new CreateReferenceExchangeReadinessCommand(ValidRequest("REF-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateReferenceExchangeReadinessHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ReadinessAllowed);
        Assert.Equal(TepReferenceExchangeReadinessState.Deferred, repository.Items.Single().ExchangeReadinessState);
        Assert.Equal(TepAuditReadinessState.Deferred, repository.Items.Single().AuditReadinessState);
        Assert.Equal("Reference exchange readiness dependency evaluation deferred.", repository.Items.Single().DeferredReason);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("national_id")]
    [InlineData("dateofbirth")]
    [InlineData("home_address")]
    [InlineData("credential_secret")]
    [InlineData("provider_payload")]
    [InlineData("pii_heavy")]
    [InlineData("marketplace_transaction")]
    [InlineData("dispute_workflow")]
    [InlineData("notification_delivery")]
    [InlineData("document_repository")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA);
        var request = ValidRequest("REF-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateReferenceExchangeReadinessCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.DoesNotContain(typeof(ReferenceExchangeReadinessRequest).GetProperties(), property => property.Name == "TenantId");
    }

    [Fact]
    public void Dependency_precondition_state_is_carried_by_dependency_states_contract()
    {
        var contractTypes = new[]
        {
            typeof(ReferenceExchangeReadinessRequest),
            typeof(ReferenceExchangeReadinessDto),
            typeof(TepReferenceExchangeMarketplaceReadinessMetadata)
        };

        foreach (var contractType in contractTypes)
        {
            var propertyNames = contractType.GetProperties().Select(property => property.Name).ToHashSet();
            Assert.Contains("DependencyStates", propertyNames);
        }
    }

    [Fact]
    public void Controller_permissions_match_reference_exchange_namespace()
    {
        Assert.NotNull(typeof(ReferenceExchangeController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(ReferenceExchangeController.GetAll), ReferenceExchangePermissions.Read);
        AssertPermission(nameof(ReferenceExchangeController.GetById), ReferenceExchangePermissions.Read);
        AssertPermission(nameof(ReferenceExchangeController.Create), ReferenceExchangePermissions.Manage);
        AssertPermission(nameof(ReferenceExchangeController.Update), ReferenceExchangePermissions.Manage);
        AssertPermission(nameof(ReferenceExchangeController.Archive), ReferenceExchangePermissions.Manage);
        AssertPermission(nameof(ReferenceExchangeController.Evaluate), ReferenceExchangePermissions.Evaluate);
        AssertPermission(nameof(ReferenceExchangeController.GetAuditMetadata), ReferenceExchangePermissions.AuditRead);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_reference_exchange_readiness", MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_reference_exchange_readiness_tenant_code_active", MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_reference_exchange_readiness_tenant_state", MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Permission_constants_do_not_use_legacy_or_candidate_identifiers()
    {
        foreach (var value in typeof(ReferenceExchangePermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null) as string))
        {
            Assert.NotNull(value);
            Assert.StartsWith("tep.reference-exchange.", value);
            Assert.DoesNotContain("CAND", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateReferenceExchangeReadinessCommand(ValidRequest("REF-EX-LE")), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, repository.Items.Single().LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var repository = new MemoryReferenceExchangeRepository();
        var handler = CreateHandler(repository, TenantA, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateReferenceExchangeReadinessCommand(ValidRequest("REF-EX-403")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    private static CreateReferenceExchangeReadinessHandler CreateHandler(MemoryReferenceExchangeRepository repository, Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

    private static CreateReferenceExchangeReadinessHandler CreateHandler(
        MemoryReferenceExchangeRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static ReferenceExchangeReadinessRequest ValidRequest(string code) =>
        new(
            code,
            "Reference exchange readiness metadata",
            TepReferenceExchangeReadinessState.Deferred,
            TepReferenceExchangeAvailabilityState.Deferred,
            TepParticipantEligibilityState.Deferred,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            TepConsentRequirementState.Deferred,
            TepVisibilityApprovalState.Deferred,
            TepDataScopeState.Deferred,
            TepDataMinimizationState.Deferred,
            TepLegalPrivacyBasisState.Deferred,
            TepEvidenceRetentionDecisionState.Deferred,
            TepAuditReadinessState.Deferred,
            TepAbuseControlState.Deferred,
            TepThrottlingPolicyState.Deferred,
            TepReviewDisputeBoundaryState.Deferred,
            TepExternalDependencyBoundaryState.Deferred,
            TepExternalDependencyBoundaryState.Deferred,
            [new ReferenceExchangeDependencyStateDto("tep.exit-reference-records", TepShellDependencyStatus.Deferred, "metadata deferred")],
            "v1",
            DateTimeOffset.UtcNow,
            1,
            "metadata deferred");

    private static ReferenceExchangeReadinessRequest ReadyRequest(string code) =>
        ValidRequest(code) with
        {
            ExchangeReadinessState = TepReferenceExchangeReadinessState.Ready,
            ExchangeAvailabilityState = TepReferenceExchangeAvailabilityState.LocalMetadata,
            ParticipantEligibilityState = TepParticipantEligibilityState.Eligible,
            AssociationMembershipReference = Guid.NewGuid(),
            VerifiedParticipantReference = Guid.NewGuid(),
            ConsentVisibilityPolicyReference = Guid.NewGuid(),
            CandidateProfileReference = Guid.NewGuid(),
            ExitReferenceRecordReference = Guid.NewGuid(),
            ReviewBoardCaseReference = Guid.NewGuid(),
            TrustLevelPolicyReference = Guid.NewGuid(),
            ConsentPreconditionState = TepConsentRequirementState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            DataScopeState = TepDataScopeState.Available,
            MinimizationState = TepDataMinimizationState.Approved,
            LegalPrivacyBasisState = TepLegalPrivacyBasisState.LocalMetadata,
            EvidenceRetentionState = TepEvidenceRetentionDecisionState.LocalMetadata,
            AuditReadinessState = TepAuditReadinessState.LocalMetadata,
            AbuseControlState = TepAbuseControlState.LocalMetadata,
            ThrottlingPolicyState = TepThrottlingPolicyState.LocalMetadata,
            ReviewDisputeBoundaryState = TepReviewDisputeBoundaryState.PreconditionSatisfied,
            NotificationDependencyState = TepExternalDependencyBoundaryState.Deferred,
            DocumentDependencyState = TepExternalDependencyBoundaryState.Deferred,
            DependencyStates = AvailableDependencyStates(),
            DeferredReason = null
        };

    private static IReadOnlyList<ReferenceExchangeDependencyStateDto> AvailableDependencyStates() =>
    [
        new("tep.association-memberships", TepShellDependencyStatus.Available, null),
        new("tep.consent-visibility-policies", TepShellDependencyStatus.Available, null),
        new("tep.verified-participants", TepShellDependencyStatus.Available, null),
        new("tep.review-board", TepShellDependencyStatus.Available, null),
        new("tep.trust-levels", TepShellDependencyStatus.Available, null),
        new("tep.candidate-profiles", TepShellDependencyStatus.Available, null),
        new("tep.exit-reference-records", TepShellDependencyStatus.Available, null)
    ];

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(ReferenceExchangeController)
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

    private sealed class MemoryReferenceExchangeRepository : ITepReferenceExchangeMarketplaceReadinessMetadataRepository
    {
        public List<TepReferenceExchangeMarketplaceReadinessMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepReferenceExchangeMarketplaceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepReferenceExchangeMarketplaceReadinessMetadata>>(
                Items.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<TepReferenceExchangeMarketplaceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(x =>
                x.TenantId == tenantId
                && x.LegalEntityId == legalEntityId
                && !x.IsDeleted
                && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                && x.Id != excludingId));

        public Task CreateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
