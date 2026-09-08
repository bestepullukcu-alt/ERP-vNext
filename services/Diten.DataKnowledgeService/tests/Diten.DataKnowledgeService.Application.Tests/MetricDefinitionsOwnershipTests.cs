using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Handlers;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class MetricDefinitionsOwnershipTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetMetricDefinitionsOwnershipReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricDefinitionsOwnershipReadinessListQuery(), CancellationToken.None);
        var get = await new GetMetricDefinitionsOwnershipReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricDefinitionsOwnershipReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("MetricDefinitionsOwnership readiness", get.Data.DisplayName);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.DefinitionCatalogBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.OwnershipAssignmentIntakeBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.StewardshipScopeBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.ApprovalControlBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.DefinitionReviewBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.MetricSemanticRegistrySourceDependencyState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.KpiCatalogSourceDependencyState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetMetricDefinitionsOwnershipReadinessByIdHandler(
            new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetMetricDefinitionsOwnershipReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(metadata);
        var handler = new DeleteMetricDefinitionsOwnershipReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteMetricDefinitionsOwnershipReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.Archived, stored.MetricDefinitionsOwnershipReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest(readinessState: MetricDefinitionsOwnershipReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.Deferred, stored.MetricDefinitionsOwnershipReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: MetricDefinitionsOwnershipReadinessState.Ready,
            dataMinimizationState: MetricDefinitionsOwnershipReadinessState.Ready,
            retentionPolicyState: MetricDefinitionsOwnershipReadinessState.Ready,
            approvalPolicyState: MetricDefinitionsOwnershipReadinessState.Ready);
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(metadata);
        var handler = new EvaluateMetricDefinitionsOwnershipReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateMetricDefinitionsOwnershipReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.Ready, response.Data!.MetricDefinitionsOwnershipReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: MetricDefinitionsOwnershipReadinessState.Deferred);
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(metadata);
        var handler = new EvaluateMetricDefinitionsOwnershipReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateMetricDefinitionsOwnershipReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(MetricDefinitionsOwnershipReadinessState.Deferred, response.Data!.MetricDefinitionsOwnershipReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(definitionCatalogBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready),
            ValidRequest(ownershipAssignmentIntakeBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready),
            ValidRequest(stewardshipScopeBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready),
            ValidRequest(approvalControlBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready),
            ValidRequest(definitionReviewBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: MetricDefinitionsOwnershipReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateMetricDefinitionsOwnershipReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(metadata);
        var response = await new GetMetricDefinitionsOwnershipAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetMetricDefinitionsOwnershipAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(MetricDefinitionsOwnershipGuard.AuditReadPermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("metric-definitions-ownership pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateMetricDefinitionsOwnershipReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(MetricDefinitionsOwnershipReadinessCreateRequest),
            typeof(MetricDefinitionsOwnershipReadinessDto),
            typeof(MetricDefinitionsOwnershipReadinessMetadata)
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
        Assert.Null(typeof(MetricDefinitionsOwnershipReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(MetricDefinitionsOwnershipGuard.ReadPermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.GetAll)));
        Assert.Equal(MetricDefinitionsOwnershipGuard.ReadPermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.GetById)));
        Assert.Equal(MetricDefinitionsOwnershipGuard.ManagePermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.Create)));
        Assert.Equal(MetricDefinitionsOwnershipGuard.EvaluatePermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.Evaluate)));
        Assert.Equal(MetricDefinitionsOwnershipGuard.ManagePermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.Delete)));
        Assert.Equal(MetricDefinitionsOwnershipGuard.AuditReadPermission, PermissionFor(nameof(MetricDefinitionsOwnershipController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_metric_definitions_ownership_readiness", MongoMetricDefinitionsOwnershipReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_metric_definitions_ownership_tenant_code_active", MongoMetricDefinitionsOwnershipReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_metric_definitions_ownership_tenant_state", MongoMetricDefinitionsOwnershipReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0060");
        var runtimeStrings = new[]
        {
            MetricDefinitionsOwnershipGuard.OwnerKey,
            MetricDefinitionsOwnershipGuard.ReadPermission,
            MetricDefinitionsOwnershipGuard.ManagePermission,
            MetricDefinitionsOwnershipGuard.EvaluatePermission,
            MetricDefinitionsOwnershipGuard.AuditReadPermission,
            MongoMetricDefinitionsOwnershipReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateMetricDefinitionsOwnershipReadinessHandler CreateHandler(
        IMetricDefinitionsOwnershipReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static MetricDefinitionsOwnershipReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "MetricDefinitionsOwnership readiness",
        MetricDefinitionsOwnershipReadinessState readinessState = MetricDefinitionsOwnershipReadinessState.Draft,
        string sourceContractVersion = "v1",
        MetricDefinitionsOwnershipReadinessState definitionCatalogBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
        MetricDefinitionsOwnershipReadinessState ownershipAssignmentIntakeBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
        MetricDefinitionsOwnershipReadinessState stewardshipScopeBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
        MetricDefinitionsOwnershipReadinessState approvalControlBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
        MetricDefinitionsOwnershipReadinessState definitionReviewBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
        MetricDefinitionsOwnershipReadinessState automatedDecisionBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            MetricDefinitionsOwnershipReadinessState = readinessState,
            DefinitionCatalogBoundaryState = definitionCatalogBoundaryState,
            OwnershipAssignmentIntakeBoundaryState = ownershipAssignmentIntakeBoundaryState,
            StewardshipScopeBoundaryState = stewardshipScopeBoundaryState,
            ApprovalControlBoundaryState = approvalControlBoundaryState,
            DefinitionReviewBoundaryState = definitionReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            KpiCatalogSourceDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            DataContractRegistryDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            NotificationDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            StewardshipPreconditionState = MetricDefinitionsOwnershipReadinessState.Deferred,
            DataMinimizationState = MetricDefinitionsOwnershipReadinessState.Deferred,
            RetentionPolicyState = MetricDefinitionsOwnershipReadinessState.Deferred,
            ApprovalPolicyState = MetricDefinitionsOwnershipReadinessState.Deferred,
            DependencyStates = new Dictionary<string, MetricDefinitionsOwnershipReadinessState>
            {
                ["talentDataSource"] = MetricDefinitionsOwnershipReadinessState.Ready,
                ["consentPolicy"] = MetricDefinitionsOwnershipReadinessState.Ready,
                ["definitionCatalog"] = MetricDefinitionsOwnershipReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            MetricDefinitionsOwnershipReadinessVersion = 1
        };

    private static MetricDefinitionsOwnershipReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        MetricDefinitionsOwnershipReadinessState stewardshipPreconditionState = MetricDefinitionsOwnershipReadinessState.Deferred,
        MetricDefinitionsOwnershipReadinessState dataMinimizationState = MetricDefinitionsOwnershipReadinessState.Deferred,
        MetricDefinitionsOwnershipReadinessState retentionPolicyState = MetricDefinitionsOwnershipReadinessState.Deferred,
        MetricDefinitionsOwnershipReadinessState approvalPolicyState = MetricDefinitionsOwnershipReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "MetricDefinitionsOwnership readiness",
            MetricDefinitionsOwnershipReadinessState = MetricDefinitionsOwnershipReadinessState.Draft,
            DefinitionCatalogBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            OwnershipAssignmentIntakeBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            StewardshipScopeBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            ApprovalControlBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            DefinitionReviewBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            MetricSemanticRegistrySourceDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            KpiCatalogSourceDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            DataContractRegistryDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            NotificationDependencyState = MetricDefinitionsOwnershipReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            ApprovalPolicyState = approvalPolicyState,
            DependencyStates = new Dictionary<string, MetricDefinitionsOwnershipReadinessState>
            {
                ["talentDataSource"] = MetricDefinitionsOwnershipReadinessState.Ready
            },
            SourceContractVersion = "v1",
            MetricDefinitionsOwnershipReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, MetricDefinitionsOwnershipReadinessMetadata> RepositoryItems(
        InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, MetricDefinitionsOwnershipReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(MetricDefinitionsOwnershipController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository : IMetricDefinitionsOwnershipReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, MetricDefinitionsOwnershipReadinessMetadata> _items;

        public InMemoryMetricDefinitionsOwnershipReadinessMetadataRepository(params MetricDefinitionsOwnershipReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<MetricDefinitionsOwnershipReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MetricDefinitionsOwnershipReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<MetricDefinitionsOwnershipReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(MetricDefinitionsOwnershipReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
