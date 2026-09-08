using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Handlers;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class MetricSemanticRegistryTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateMetricSemanticRegistryReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetMetricSemanticRegistryReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricSemanticRegistryReadinessListQuery(), CancellationToken.None);
        var get = await new GetMetricSemanticRegistryReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricSemanticRegistryReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("MetricSemanticRegistry readiness", get.Data.DisplayName);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.MetricIdentityCatalogBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.SemanticEntityIntakeBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.DimensionMeasureScopeBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.SemanticBindingControlBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.RegistryReviewBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.DataSourceRegistryDependencyState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.DataGovernancePolicyDependencyState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.SemanticContractSourceDependencyState);
        Assert.Equal(MetricSemanticRegistryReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetMetricSemanticRegistryReadinessByIdHandler(
            new InMemoryMetricSemanticRegistryReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetMetricSemanticRegistryReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryMetricSemanticRegistryReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateMetricSemanticRegistryReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateMetricSemanticRegistryReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateMetricSemanticRegistryReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository(metadata);
        var handler = new DeleteMetricSemanticRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteMetricSemanticRegistryReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(MetricSemanticRegistryReadinessState.Archived, stored.MetricSemanticRegistryReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateMetricSemanticRegistryReadinessCommand(ValidRequest(readinessState: MetricSemanticRegistryReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricSemanticRegistryReadinessState.Deferred, stored.MetricSemanticRegistryReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: MetricSemanticRegistryReadinessState.Ready,
            dataMinimizationState: MetricSemanticRegistryReadinessState.Ready,
            retentionPolicyState: MetricSemanticRegistryReadinessState.Ready,
            versioningPolicyState: MetricSemanticRegistryReadinessState.Ready);
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository(metadata);
        var handler = new EvaluateMetricSemanticRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateMetricSemanticRegistryReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricSemanticRegistryReadinessState.Ready, response.Data!.MetricSemanticRegistryReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: MetricSemanticRegistryReadinessState.Deferred);
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository(metadata);
        var handler = new EvaluateMetricSemanticRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateMetricSemanticRegistryReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricSemanticRegistryReadinessState.Deferred, response.Data!.MetricSemanticRegistryReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryMetricSemanticRegistryReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(metricIdentityCatalogBoundaryState: MetricSemanticRegistryReadinessState.Ready),
            ValidRequest(semanticEntityIntakeBoundaryState: MetricSemanticRegistryReadinessState.Ready),
            ValidRequest(dimensionMeasureScopeBoundaryState: MetricSemanticRegistryReadinessState.Ready),
            ValidRequest(semanticBindingControlBoundaryState: MetricSemanticRegistryReadinessState.Ready),
            ValidRequest(registryReviewBoundaryState: MetricSemanticRegistryReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: MetricSemanticRegistryReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateMetricSemanticRegistryReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryMetricSemanticRegistryReadinessMetadataRepository(metadata);
        var response = await new GetMetricSemanticRegistryAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricSemanticRegistryAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(MetricSemanticRegistryGuard.AuditReadPermission, PermissionFor(nameof(MetricSemanticRegistryController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryMetricSemanticRegistryReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateMetricSemanticRegistryReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("metric-semantic-registry pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryMetricSemanticRegistryReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateMetricSemanticRegistryReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(MetricSemanticRegistryReadinessCreateRequest),
            typeof(MetricSemanticRegistryReadinessDto),
            typeof(MetricSemanticRegistryReadinessMetadata)
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
        Assert.Null(typeof(MetricSemanticRegistryReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(MetricSemanticRegistryGuard.ReadPermission, PermissionFor(nameof(MetricSemanticRegistryController.GetAll)));
        Assert.Equal(MetricSemanticRegistryGuard.ReadPermission, PermissionFor(nameof(MetricSemanticRegistryController.GetById)));
        Assert.Equal(MetricSemanticRegistryGuard.ManagePermission, PermissionFor(nameof(MetricSemanticRegistryController.Create)));
        Assert.Equal(MetricSemanticRegistryGuard.EvaluatePermission, PermissionFor(nameof(MetricSemanticRegistryController.Evaluate)));
        Assert.Equal(MetricSemanticRegistryGuard.ManagePermission, PermissionFor(nameof(MetricSemanticRegistryController.Delete)));
        Assert.Equal(MetricSemanticRegistryGuard.AuditReadPermission, PermissionFor(nameof(MetricSemanticRegistryController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_metric_semantic_registry_readiness", MongoMetricSemanticRegistryReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_metric_semantic_registry_tenant_code_active", MongoMetricSemanticRegistryReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_metric_semantic_registry_tenant_state", MongoMetricSemanticRegistryReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0004");
        var runtimeStrings = new[]
        {
            MetricSemanticRegistryGuard.OwnerKey,
            MetricSemanticRegistryGuard.ReadPermission,
            MetricSemanticRegistryGuard.ManagePermission,
            MetricSemanticRegistryGuard.EvaluatePermission,
            MetricSemanticRegistryGuard.AuditReadPermission,
            MongoMetricSemanticRegistryReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateMetricSemanticRegistryReadinessHandler CreateHandler(
        IMetricSemanticRegistryReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static MetricSemanticRegistryReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "MetricSemanticRegistry readiness",
        MetricSemanticRegistryReadinessState readinessState = MetricSemanticRegistryReadinessState.Draft,
        string sourceContractVersion = "v1",
        MetricSemanticRegistryReadinessState metricIdentityCatalogBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
        MetricSemanticRegistryReadinessState semanticEntityIntakeBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
        MetricSemanticRegistryReadinessState dimensionMeasureScopeBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
        MetricSemanticRegistryReadinessState semanticBindingControlBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
        MetricSemanticRegistryReadinessState registryReviewBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
        MetricSemanticRegistryReadinessState automatedDecisionBoundaryState = MetricSemanticRegistryReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            MetricSemanticRegistryReadinessState = readinessState,
            MetricIdentityCatalogBoundaryState = metricIdentityCatalogBoundaryState,
            SemanticEntityIntakeBoundaryState = semanticEntityIntakeBoundaryState,
            DimensionMeasureScopeBoundaryState = dimensionMeasureScopeBoundaryState,
            SemanticBindingControlBoundaryState = semanticBindingControlBoundaryState,
            RegistryReviewBoundaryState = registryReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            DataSourceRegistryDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            SemanticContractSourceDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            NotificationDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            StewardshipPreconditionState = MetricSemanticRegistryReadinessState.Deferred,
            DataMinimizationState = MetricSemanticRegistryReadinessState.Deferred,
            RetentionPolicyState = MetricSemanticRegistryReadinessState.Deferred,
            VersioningPolicyState = MetricSemanticRegistryReadinessState.Deferred,
            DependencyStates = new Dictionary<string, MetricSemanticRegistryReadinessState>
            {
                ["talentDataSource"] = MetricSemanticRegistryReadinessState.Ready,
                ["consentPolicy"] = MetricSemanticRegistryReadinessState.Ready,
                ["metricIdentityCatalog"] = MetricSemanticRegistryReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            MetricSemanticRegistryReadinessVersion = 1
        };

    private static MetricSemanticRegistryReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        MetricSemanticRegistryReadinessState stewardshipPreconditionState = MetricSemanticRegistryReadinessState.Deferred,
        MetricSemanticRegistryReadinessState dataMinimizationState = MetricSemanticRegistryReadinessState.Deferred,
        MetricSemanticRegistryReadinessState retentionPolicyState = MetricSemanticRegistryReadinessState.Deferred,
        MetricSemanticRegistryReadinessState versioningPolicyState = MetricSemanticRegistryReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "MetricSemanticRegistry readiness",
            MetricSemanticRegistryReadinessState = MetricSemanticRegistryReadinessState.Draft,
            MetricIdentityCatalogBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            SemanticEntityIntakeBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            DimensionMeasureScopeBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            SemanticBindingControlBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            RegistryReviewBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = MetricSemanticRegistryReadinessState.NotRequired,
            DataSourceRegistryDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            SemanticContractSourceDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            NotificationDependencyState = MetricSemanticRegistryReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            VersioningPolicyState = versioningPolicyState,
            DependencyStates = new Dictionary<string, MetricSemanticRegistryReadinessState>
            {
                ["talentDataSource"] = MetricSemanticRegistryReadinessState.Ready
            },
            SourceContractVersion = "v1",
            MetricSemanticRegistryReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, MetricSemanticRegistryReadinessMetadata> RepositoryItems(
        InMemoryMetricSemanticRegistryReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryMetricSemanticRegistryReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, MetricSemanticRegistryReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(MetricSemanticRegistryController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryMetricSemanticRegistryReadinessMetadataRepository : IMetricSemanticRegistryReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, MetricSemanticRegistryReadinessMetadata> _items;

        public InMemoryMetricSemanticRegistryReadinessMetadataRepository(params MetricSemanticRegistryReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<MetricSemanticRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MetricSemanticRegistryReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<MetricSemanticRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(MetricSemanticRegistryReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
