using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Handlers;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class TalentDataFoundationTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateTalentDataFoundationReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetTalentDataFoundationReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetTalentDataFoundationReadinessListQuery(), CancellationToken.None);
        var get = await new GetTalentDataFoundationReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetTalentDataFoundationReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("TalentDataFoundation readiness", get.Data.DisplayName);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.TalentEntityCatalogBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.DataIngestionBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.IdentityResolutionBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.DataQualityBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.LineageTrackingBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.HcmFoundationDependencyState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(TalentDataFoundationReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetTalentDataFoundationReadinessByIdHandler(
            new InMemoryTalentDataFoundationReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetTalentDataFoundationReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryTalentDataFoundationReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateTalentDataFoundationReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateTalentDataFoundationReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateTalentDataFoundationReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository(metadata);
        var handler = new DeleteTalentDataFoundationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteTalentDataFoundationReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TalentDataFoundationReadinessState.Archived, stored.TalentDataFoundationReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateTalentDataFoundationReadinessCommand(ValidRequest(readinessState: TalentDataFoundationReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDataFoundationReadinessState.Deferred, stored.TalentDataFoundationReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: TalentDataFoundationReadinessState.Ready,
            dataMinimizationState: TalentDataFoundationReadinessState.Ready,
            retentionPolicyState: TalentDataFoundationReadinessState.Ready,
            evidencePolicyState: TalentDataFoundationReadinessState.Ready);
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository(metadata);
        var handler = new EvaluateTalentDataFoundationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateTalentDataFoundationReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDataFoundationReadinessState.Ready, response.Data!.TalentDataFoundationReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: TalentDataFoundationReadinessState.Deferred);
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository(metadata);
        var handler = new EvaluateTalentDataFoundationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateTalentDataFoundationReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDataFoundationReadinessState.Deferred, response.Data!.TalentDataFoundationReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryTalentDataFoundationReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(talentEntityCatalogBoundaryState: TalentDataFoundationReadinessState.Ready),
            ValidRequest(dataIngestionBoundaryState: TalentDataFoundationReadinessState.Ready),
            ValidRequest(identityResolutionBoundaryState: TalentDataFoundationReadinessState.Ready),
            ValidRequest(dataQualityBoundaryState: TalentDataFoundationReadinessState.Ready),
            ValidRequest(lineageTrackingBoundaryState: TalentDataFoundationReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: TalentDataFoundationReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateTalentDataFoundationReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryTalentDataFoundationReadinessMetadataRepository(metadata);
        var response = await new GetTalentDataFoundationAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetTalentDataFoundationAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(TalentDataFoundationGuard.AuditReadPermission, PermissionFor(nameof(TalentDataFoundationController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryTalentDataFoundationReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateTalentDataFoundationReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("talent-data-foundation pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryTalentDataFoundationReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateTalentDataFoundationReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(TalentDataFoundationReadinessCreateRequest),
            typeof(TalentDataFoundationReadinessDto),
            typeof(TalentDataFoundationReadinessMetadata)
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
        Assert.Null(typeof(TalentDataFoundationReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(TalentDataFoundationGuard.ReadPermission, PermissionFor(nameof(TalentDataFoundationController.GetAll)));
        Assert.Equal(TalentDataFoundationGuard.ReadPermission, PermissionFor(nameof(TalentDataFoundationController.GetById)));
        Assert.Equal(TalentDataFoundationGuard.ManagePermission, PermissionFor(nameof(TalentDataFoundationController.Create)));
        Assert.Equal(TalentDataFoundationGuard.EvaluatePermission, PermissionFor(nameof(TalentDataFoundationController.Evaluate)));
        Assert.Equal(TalentDataFoundationGuard.ManagePermission, PermissionFor(nameof(TalentDataFoundationController.Delete)));
        Assert.Equal(TalentDataFoundationGuard.AuditReadPermission, PermissionFor(nameof(TalentDataFoundationController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_talent_data_foundation_readiness", MongoTalentDataFoundationReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_talent_data_foundation_tenant_code_active", MongoTalentDataFoundationReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_talent_data_foundation_tenant_state", MongoTalentDataFoundationReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0041");
        var legacy = string.Join("-", "MOD", "0336");
        var runtimeStrings = new[]
        {
            TalentDataFoundationGuard.OwnerKey,
            TalentDataFoundationGuard.ReadPermission,
            TalentDataFoundationGuard.ManagePermission,
            TalentDataFoundationGuard.EvaluatePermission,
            TalentDataFoundationGuard.AuditReadPermission,
            MongoTalentDataFoundationReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateTalentDataFoundationReadinessHandler CreateHandler(
        ITalentDataFoundationReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static TalentDataFoundationReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "TalentDataFoundation readiness",
        TalentDataFoundationReadinessState readinessState = TalentDataFoundationReadinessState.Draft,
        string sourceContractVersion = "v1",
        TalentDataFoundationReadinessState talentEntityCatalogBoundaryState = TalentDataFoundationReadinessState.NotRequired,
        TalentDataFoundationReadinessState dataIngestionBoundaryState = TalentDataFoundationReadinessState.NotRequired,
        TalentDataFoundationReadinessState identityResolutionBoundaryState = TalentDataFoundationReadinessState.NotRequired,
        TalentDataFoundationReadinessState dataQualityBoundaryState = TalentDataFoundationReadinessState.NotRequired,
        TalentDataFoundationReadinessState lineageTrackingBoundaryState = TalentDataFoundationReadinessState.NotRequired,
        TalentDataFoundationReadinessState automatedDecisionBoundaryState = TalentDataFoundationReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            TalentDataFoundationReadinessState = readinessState,
            TalentEntityCatalogBoundaryState = talentEntityCatalogBoundaryState,
            DataIngestionBoundaryState = dataIngestionBoundaryState,
            IdentityResolutionBoundaryState = identityResolutionBoundaryState,
            DataQualityBoundaryState = dataQualityBoundaryState,
            LineageTrackingBoundaryState = lineageTrackingBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            HcmFoundationDependencyState = TalentDataFoundationReadinessState.NotRequired,
            ConsentPolicyDependencyState = TalentDataFoundationReadinessState.NotRequired,
            DocumentDependencyState = TalentDataFoundationReadinessState.NotRequired,
            NotificationDependencyState = TalentDataFoundationReadinessState.NotRequired,
            ConsentPreconditionState = TalentDataFoundationReadinessState.Deferred,
            DataMinimizationState = TalentDataFoundationReadinessState.Deferred,
            RetentionPolicyState = TalentDataFoundationReadinessState.Deferred,
            EvidencePolicyState = TalentDataFoundationReadinessState.Deferred,
            DependencyStates = new Dictionary<string, TalentDataFoundationReadinessState>
            {
                ["hcmFoundation"] = TalentDataFoundationReadinessState.Ready,
                ["consentPolicy"] = TalentDataFoundationReadinessState.Ready,
                ["talentEntityCatalog"] = TalentDataFoundationReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            TalentDataFoundationReadinessVersion = 1
        };

    private static TalentDataFoundationReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        TalentDataFoundationReadinessState consentPreconditionState = TalentDataFoundationReadinessState.Deferred,
        TalentDataFoundationReadinessState dataMinimizationState = TalentDataFoundationReadinessState.Deferred,
        TalentDataFoundationReadinessState retentionPolicyState = TalentDataFoundationReadinessState.Deferred,
        TalentDataFoundationReadinessState evidencePolicyState = TalentDataFoundationReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "TalentDataFoundation readiness",
            TalentDataFoundationReadinessState = TalentDataFoundationReadinessState.Draft,
            TalentEntityCatalogBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            DataIngestionBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            IdentityResolutionBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            DataQualityBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            LineageTrackingBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = TalentDataFoundationReadinessState.NotRequired,
            HcmFoundationDependencyState = TalentDataFoundationReadinessState.NotRequired,
            ConsentPolicyDependencyState = TalentDataFoundationReadinessState.NotRequired,
            DocumentDependencyState = TalentDataFoundationReadinessState.NotRequired,
            NotificationDependencyState = TalentDataFoundationReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, TalentDataFoundationReadinessState>
            {
                ["hcmFoundation"] = TalentDataFoundationReadinessState.Ready
            },
            SourceContractVersion = "v1",
            TalentDataFoundationReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, TalentDataFoundationReadinessMetadata> RepositoryItems(
        InMemoryTalentDataFoundationReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryTalentDataFoundationReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, TalentDataFoundationReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(TalentDataFoundationController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryTalentDataFoundationReadinessMetadataRepository : ITalentDataFoundationReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, TalentDataFoundationReadinessMetadata> _items;

        public InMemoryTalentDataFoundationReadinessMetadataRepository(params TalentDataFoundationReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<TalentDataFoundationReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TalentDataFoundationReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<TalentDataFoundationReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
