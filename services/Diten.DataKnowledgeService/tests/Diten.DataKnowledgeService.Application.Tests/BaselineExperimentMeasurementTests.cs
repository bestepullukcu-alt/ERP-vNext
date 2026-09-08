using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Commands;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Handlers;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class BaselineExperimentMeasurementTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetBaselineExperimentMeasurementReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetBaselineExperimentMeasurementReadinessListQuery(), CancellationToken.None);
        var get = await new GetBaselineExperimentMeasurementReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetBaselineExperimentMeasurementReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("BaselineExperimentMeasurement readiness", get.Data.DisplayName);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.BaselineCatalogBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.ExperimentDesignIntakeBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.MeasurementBindingScopeBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.ResultPublicationControlBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.ExperimentReviewBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.MetricSemanticRegistrySourceDependencyState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.ScorecardSourceDependencyState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetBaselineExperimentMeasurementReadinessByIdHandler(
            new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetBaselineExperimentMeasurementReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(metadata);
        var handler = new DeleteBaselineExperimentMeasurementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteBaselineExperimentMeasurementReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.Archived, stored.BaselineExperimentMeasurementReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest(readinessState: BaselineExperimentMeasurementReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.Deferred, stored.BaselineExperimentMeasurementReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: BaselineExperimentMeasurementReadinessState.Ready,
            dataMinimizationState: BaselineExperimentMeasurementReadinessState.Ready,
            retentionPolicyState: BaselineExperimentMeasurementReadinessState.Ready,
            measurementPolicyState: BaselineExperimentMeasurementReadinessState.Ready);
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(metadata);
        var handler = new EvaluateBaselineExperimentMeasurementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateBaselineExperimentMeasurementReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.Ready, response.Data!.BaselineExperimentMeasurementReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: BaselineExperimentMeasurementReadinessState.Deferred);
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(metadata);
        var handler = new EvaluateBaselineExperimentMeasurementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateBaselineExperimentMeasurementReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineExperimentMeasurementReadinessState.Deferred, response.Data!.BaselineExperimentMeasurementReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(baselineCatalogBoundaryState: BaselineExperimentMeasurementReadinessState.Ready),
            ValidRequest(experimentDesignIntakeBoundaryState: BaselineExperimentMeasurementReadinessState.Ready),
            ValidRequest(measurementBindingScopeBoundaryState: BaselineExperimentMeasurementReadinessState.Ready),
            ValidRequest(resultPublicationControlBoundaryState: BaselineExperimentMeasurementReadinessState.Ready),
            ValidRequest(experimentReviewBoundaryState: BaselineExperimentMeasurementReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: BaselineExperimentMeasurementReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateBaselineExperimentMeasurementReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(metadata);
        var response = await new GetBaselineExperimentMeasurementAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetBaselineExperimentMeasurementAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(BaselineExperimentMeasurementGuard.AuditReadPermission, PermissionFor(nameof(BaselineExperimentMeasurementController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("baseline-experiment-measurement pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateBaselineExperimentMeasurementReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(BaselineExperimentMeasurementReadinessCreateRequest),
            typeof(BaselineExperimentMeasurementReadinessDto),
            typeof(BaselineExperimentMeasurementReadinessMetadata)
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
        Assert.Null(typeof(BaselineExperimentMeasurementReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(BaselineExperimentMeasurementGuard.ReadPermission, PermissionFor(nameof(BaselineExperimentMeasurementController.GetAll)));
        Assert.Equal(BaselineExperimentMeasurementGuard.ReadPermission, PermissionFor(nameof(BaselineExperimentMeasurementController.GetById)));
        Assert.Equal(BaselineExperimentMeasurementGuard.ManagePermission, PermissionFor(nameof(BaselineExperimentMeasurementController.Create)));
        Assert.Equal(BaselineExperimentMeasurementGuard.EvaluatePermission, PermissionFor(nameof(BaselineExperimentMeasurementController.Evaluate)));
        Assert.Equal(BaselineExperimentMeasurementGuard.ManagePermission, PermissionFor(nameof(BaselineExperimentMeasurementController.Delete)));
        Assert.Equal(BaselineExperimentMeasurementGuard.AuditReadPermission, PermissionFor(nameof(BaselineExperimentMeasurementController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_baseline_experiment_measurement_readiness", MongoBaselineExperimentMeasurementReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_baseline_experiment_measurement_tenant_code_active", MongoBaselineExperimentMeasurementReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_baseline_experiment_measurement_tenant_state", MongoBaselineExperimentMeasurementReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0062");
        var runtimeStrings = new[]
        {
            BaselineExperimentMeasurementGuard.OwnerKey,
            BaselineExperimentMeasurementGuard.ReadPermission,
            BaselineExperimentMeasurementGuard.ManagePermission,
            BaselineExperimentMeasurementGuard.EvaluatePermission,
            BaselineExperimentMeasurementGuard.AuditReadPermission,
            MongoBaselineExperimentMeasurementReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateBaselineExperimentMeasurementReadinessHandler CreateHandler(
        IBaselineExperimentMeasurementReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static BaselineExperimentMeasurementReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "BaselineExperimentMeasurement readiness",
        BaselineExperimentMeasurementReadinessState readinessState = BaselineExperimentMeasurementReadinessState.Draft,
        string sourceContractVersion = "v1",
        BaselineExperimentMeasurementReadinessState baselineCatalogBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
        BaselineExperimentMeasurementReadinessState experimentDesignIntakeBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
        BaselineExperimentMeasurementReadinessState measurementBindingScopeBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
        BaselineExperimentMeasurementReadinessState resultPublicationControlBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
        BaselineExperimentMeasurementReadinessState experimentReviewBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
        BaselineExperimentMeasurementReadinessState automatedDecisionBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            BaselineExperimentMeasurementReadinessState = readinessState,
            BaselineCatalogBoundaryState = baselineCatalogBoundaryState,
            ExperimentDesignIntakeBoundaryState = experimentDesignIntakeBoundaryState,
            MeasurementBindingScopeBoundaryState = measurementBindingScopeBoundaryState,
            ResultPublicationControlBoundaryState = resultPublicationControlBoundaryState,
            ExperimentReviewBoundaryState = experimentReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            ScorecardSourceDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            DataContractRegistryDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            NotificationDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            StewardshipPreconditionState = BaselineExperimentMeasurementReadinessState.Deferred,
            DataMinimizationState = BaselineExperimentMeasurementReadinessState.Deferred,
            RetentionPolicyState = BaselineExperimentMeasurementReadinessState.Deferred,
            MeasurementPolicyState = BaselineExperimentMeasurementReadinessState.Deferred,
            DependencyStates = new Dictionary<string, BaselineExperimentMeasurementReadinessState>
            {
                ["talentDataSource"] = BaselineExperimentMeasurementReadinessState.Ready,
                ["consentPolicy"] = BaselineExperimentMeasurementReadinessState.Ready,
                ["baselineCatalog"] = BaselineExperimentMeasurementReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            BaselineExperimentMeasurementReadinessVersion = 1
        };

    private static BaselineExperimentMeasurementReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        BaselineExperimentMeasurementReadinessState stewardshipPreconditionState = BaselineExperimentMeasurementReadinessState.Deferred,
        BaselineExperimentMeasurementReadinessState dataMinimizationState = BaselineExperimentMeasurementReadinessState.Deferred,
        BaselineExperimentMeasurementReadinessState retentionPolicyState = BaselineExperimentMeasurementReadinessState.Deferred,
        BaselineExperimentMeasurementReadinessState measurementPolicyState = BaselineExperimentMeasurementReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "BaselineExperimentMeasurement readiness",
            BaselineExperimentMeasurementReadinessState = BaselineExperimentMeasurementReadinessState.Draft,
            BaselineCatalogBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            ExperimentDesignIntakeBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            MeasurementBindingScopeBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            ResultPublicationControlBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            ExperimentReviewBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = BaselineExperimentMeasurementReadinessState.NotRequired,
            MetricSemanticRegistrySourceDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            ScorecardSourceDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            DataContractRegistryDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            NotificationDependencyState = BaselineExperimentMeasurementReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            MeasurementPolicyState = measurementPolicyState,
            DependencyStates = new Dictionary<string, BaselineExperimentMeasurementReadinessState>
            {
                ["talentDataSource"] = BaselineExperimentMeasurementReadinessState.Ready
            },
            SourceContractVersion = "v1",
            BaselineExperimentMeasurementReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, BaselineExperimentMeasurementReadinessMetadata> RepositoryItems(
        InMemoryBaselineExperimentMeasurementReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryBaselineExperimentMeasurementReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, BaselineExperimentMeasurementReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(BaselineExperimentMeasurementController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryBaselineExperimentMeasurementReadinessMetadataRepository : IBaselineExperimentMeasurementReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, BaselineExperimentMeasurementReadinessMetadata> _items;

        public InMemoryBaselineExperimentMeasurementReadinessMetadataRepository(params BaselineExperimentMeasurementReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<BaselineExperimentMeasurementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BaselineExperimentMeasurementReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<BaselineExperimentMeasurementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
