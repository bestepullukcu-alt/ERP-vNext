using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Commands;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Handlers;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class ProfessionalReputationLedgerTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetProfessionalReputationLedgerReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetProfessionalReputationLedgerReadinessListQuery(), CancellationToken.None);
        var get = await new GetProfessionalReputationLedgerReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetProfessionalReputationLedgerReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("ProfessionalReputationLedger readiness", get.Data.DisplayName);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.ReputationSignalCatalogBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.EndorsementIntakeBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.AttributionScopeBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.SignalReviewBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetProfessionalReputationLedgerReadinessByIdHandler(
            new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetProfessionalReputationLedgerReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(metadata);
        var handler = new DeleteProfessionalReputationLedgerReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteProfessionalReputationLedgerReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.Archived, stored.ProfessionalReputationLedgerReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest(readinessState: ProfessionalReputationLedgerReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.Deferred, stored.ProfessionalReputationLedgerReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: ProfessionalReputationLedgerReadinessState.Ready,
            dataMinimizationState: ProfessionalReputationLedgerReadinessState.Ready,
            retentionPolicyState: ProfessionalReputationLedgerReadinessState.Ready,
            evidencePolicyState: ProfessionalReputationLedgerReadinessState.Ready);
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(metadata);
        var handler = new EvaluateProfessionalReputationLedgerReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateProfessionalReputationLedgerReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.Ready, response.Data!.ProfessionalReputationLedgerReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: ProfessionalReputationLedgerReadinessState.Deferred);
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(metadata);
        var handler = new EvaluateProfessionalReputationLedgerReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateProfessionalReputationLedgerReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProfessionalReputationLedgerReadinessState.Deferred, response.Data!.ProfessionalReputationLedgerReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(reputationSignalCatalogBoundaryState: ProfessionalReputationLedgerReadinessState.Ready),
            ValidRequest(endorsementIntakeBoundaryState: ProfessionalReputationLedgerReadinessState.Ready),
            ValidRequest(attributionScopeBoundaryState: ProfessionalReputationLedgerReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: ProfessionalReputationLedgerReadinessState.Ready),
            ValidRequest(signalReviewBoundaryState: ProfessionalReputationLedgerReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: ProfessionalReputationLedgerReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateProfessionalReputationLedgerReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(metadata);
        var response = await new GetProfessionalReputationLedgerAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetProfessionalReputationLedgerAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(ProfessionalReputationLedgerGuard.AuditReadPermission, PermissionFor(nameof(ProfessionalReputationLedgerController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("workflow_body")]
    [InlineData("review_note")]
    [InlineData("appraisal_narrative")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    [InlineData("score")]
    [InlineData("rating")]
    [InlineData("calibration")]
    [InlineData("rank")]
    [InlineData("model_output")]
    [InlineData("automated_decision")]
    [InlineData("salary")]
    [InlineData("payroll")]
    [InlineData("benefits_election")]
    [InlineData("password")]
    [InlineData("tax")]
    public async Task Forbidden_workflow_scoring_rating_calibration_ranking_decision_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("professional-reputation-ledger pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryProfessionalReputationLedgerReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateProfessionalReputationLedgerReadinessCommand(ValidRequest(displayName: legitimateValue)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_payload_and_sensitive_fields()
    {
        var forbiddenFragments = new[]
        {
            "WorkflowBody",
            "ManagerNote",
            "HrNote",
            "EmployeeStatement",
            "ReviewNote",
            "Appraisal",
            "FreeText",
            "Narrative",
            "GoalScore",
            "RatingValue",
            "RankValue",
            "CalibrationOutcome",
            "ModelOutput",
            "AutomatedDecisionResult",
            "Amount",
            "Salary",
            "Wage",
            "Bank",
            "TaxDetail",
            "TaxIdentifier",
            "PayrollDetail",
            "BenefitsElection",
            "Payload",
            "Credential",
            "Token",
            "Secret",
            "Password",
            "National",
            "Birth",
            "HomeAddress",
            "Biometric",
            "Geolocation"
        };
        var contractTypes = new[]
        {
            typeof(ProfessionalReputationLedgerReadinessCreateRequest),
            typeof(ProfessionalReputationLedgerReadinessDto),
            typeof(ProfessionalReputationLedgerReadinessMetadata)
        };

        var names = contractTypes
            .SelectMany(type => type.GetProperties().Select(property => property.Name))
            .ToList();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(names, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.Null(typeof(ProfessionalReputationLedgerReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(ProfessionalReputationLedgerGuard.ReadPermission, PermissionFor(nameof(ProfessionalReputationLedgerController.GetAll)));
        Assert.Equal(ProfessionalReputationLedgerGuard.ReadPermission, PermissionFor(nameof(ProfessionalReputationLedgerController.GetById)));
        Assert.Equal(ProfessionalReputationLedgerGuard.ManagePermission, PermissionFor(nameof(ProfessionalReputationLedgerController.Create)));
        Assert.Equal(ProfessionalReputationLedgerGuard.EvaluatePermission, PermissionFor(nameof(ProfessionalReputationLedgerController.Evaluate)));
        Assert.Equal(ProfessionalReputationLedgerGuard.ManagePermission, PermissionFor(nameof(ProfessionalReputationLedgerController.Delete)));
        Assert.Equal(ProfessionalReputationLedgerGuard.AuditReadPermission, PermissionFor(nameof(ProfessionalReputationLedgerController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_professional_reputation_ledger_readiness", MongoProfessionalReputationLedgerReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_professional_reputation_ledger_tenant_code_active", MongoProfessionalReputationLedgerReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_professional_reputation_ledger_tenant_state", MongoProfessionalReputationLedgerReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0045");
        var legacy = string.Join("-", "MOD", "0335");
        var runtimeStrings = new[]
        {
            ProfessionalReputationLedgerGuard.OwnerKey,
            ProfessionalReputationLedgerGuard.ReadPermission,
            ProfessionalReputationLedgerGuard.ManagePermission,
            ProfessionalReputationLedgerGuard.EvaluatePermission,
            ProfessionalReputationLedgerGuard.AuditReadPermission,
            MongoProfessionalReputationLedgerReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateProfessionalReputationLedgerReadinessHandler CreateHandler(
        IProfessionalReputationLedgerReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static ProfessionalReputationLedgerReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "ProfessionalReputationLedger readiness",
        ProfessionalReputationLedgerReadinessState readinessState = ProfessionalReputationLedgerReadinessState.Draft,
        string sourceContractVersion = "v1",
        ProfessionalReputationLedgerReadinessState reputationSignalCatalogBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
        ProfessionalReputationLedgerReadinessState endorsementIntakeBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
        ProfessionalReputationLedgerReadinessState attributionScopeBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
        ProfessionalReputationLedgerReadinessState visibilityControlBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
        ProfessionalReputationLedgerReadinessState signalReviewBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
        ProfessionalReputationLedgerReadinessState automatedDecisionBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            ProfessionalReputationLedgerReadinessState = readinessState,
            ReputationSignalCatalogBoundaryState = reputationSignalCatalogBoundaryState,
            EndorsementIntakeBoundaryState = endorsementIntakeBoundaryState,
            AttributionScopeBoundaryState = attributionScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            SignalReviewBoundaryState = signalReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            ConsentPolicyDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            DocumentDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            NotificationDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            ConsentPreconditionState = ProfessionalReputationLedgerReadinessState.Deferred,
            DataMinimizationState = ProfessionalReputationLedgerReadinessState.Deferred,
            RetentionPolicyState = ProfessionalReputationLedgerReadinessState.Deferred,
            EvidencePolicyState = ProfessionalReputationLedgerReadinessState.Deferred,
            DependencyStates = new Dictionary<string, ProfessionalReputationLedgerReadinessState>
            {
                ["talentDataSource"] = ProfessionalReputationLedgerReadinessState.Ready,
                ["consentPolicy"] = ProfessionalReputationLedgerReadinessState.Ready,
                ["reputationSignalCatalog"] = ProfessionalReputationLedgerReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            ProfessionalReputationLedgerReadinessVersion = 1
        };

    private static ProfessionalReputationLedgerReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        ProfessionalReputationLedgerReadinessState consentPreconditionState = ProfessionalReputationLedgerReadinessState.Deferred,
        ProfessionalReputationLedgerReadinessState dataMinimizationState = ProfessionalReputationLedgerReadinessState.Deferred,
        ProfessionalReputationLedgerReadinessState retentionPolicyState = ProfessionalReputationLedgerReadinessState.Deferred,
        ProfessionalReputationLedgerReadinessState evidencePolicyState = ProfessionalReputationLedgerReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "ProfessionalReputationLedger readiness",
            ProfessionalReputationLedgerReadinessState = ProfessionalReputationLedgerReadinessState.Draft,
            ReputationSignalCatalogBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            EndorsementIntakeBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            AttributionScopeBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            VisibilityControlBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            SignalReviewBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = ProfessionalReputationLedgerReadinessState.NotRequired,
            TalentDataSourceDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            ConsentPolicyDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            DocumentDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            NotificationDependencyState = ProfessionalReputationLedgerReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, ProfessionalReputationLedgerReadinessState>
            {
                ["talentDataSource"] = ProfessionalReputationLedgerReadinessState.Ready
            },
            SourceContractVersion = "v1",
            ProfessionalReputationLedgerReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, ProfessionalReputationLedgerReadinessMetadata> RepositoryItems(
        InMemoryProfessionalReputationLedgerReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryProfessionalReputationLedgerReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, ProfessionalReputationLedgerReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(ProfessionalReputationLedgerController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryProfessionalReputationLedgerReadinessMetadataRepository : IProfessionalReputationLedgerReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, ProfessionalReputationLedgerReadinessMetadata> _items;

        public InMemoryProfessionalReputationLedgerReadinessMetadataRepository(params ProfessionalReputationLedgerReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<ProfessionalReputationLedgerReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProfessionalReputationLedgerReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<ProfessionalReputationLedgerReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
