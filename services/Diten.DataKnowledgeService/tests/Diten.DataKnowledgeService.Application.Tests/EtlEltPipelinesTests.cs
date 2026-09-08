using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Handlers;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class EtlEltPipelinesTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateEtlEltPipelinesReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetEtlEltPipelinesReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEtlEltPipelinesReadinessListQuery(), CancellationToken.None);
        var get = await new GetEtlEltPipelinesReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEtlEltPipelinesReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("EtlEltPipelines readiness", get.Data.DisplayName);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.PipelineDefinitionCatalogBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.ExtractIntakeBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.TransformScopeBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.LoadControlBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.OrchestrationReviewBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.LakehouseSourceDependencyState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.JobOrchestrationDependencyState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(EtlEltPipelinesReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetEtlEltPipelinesReadinessByIdHandler(
            new InMemoryEtlEltPipelinesReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetEtlEltPipelinesReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryEtlEltPipelinesReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateEtlEltPipelinesReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateEtlEltPipelinesReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateEtlEltPipelinesReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository(metadata);
        var handler = new DeleteEtlEltPipelinesReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteEtlEltPipelinesReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(EtlEltPipelinesReadinessState.Archived, stored.EtlEltPipelinesReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateEtlEltPipelinesReadinessCommand(ValidRequest(readinessState: EtlEltPipelinesReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(EtlEltPipelinesReadinessState.Deferred, stored.EtlEltPipelinesReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: EtlEltPipelinesReadinessState.Ready,
            dataMinimizationState: EtlEltPipelinesReadinessState.Ready,
            retentionPolicyState: EtlEltPipelinesReadinessState.Ready,
            monitoringPolicyState: EtlEltPipelinesReadinessState.Ready);
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository(metadata);
        var handler = new EvaluateEtlEltPipelinesReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEtlEltPipelinesReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EtlEltPipelinesReadinessState.Ready, response.Data!.EtlEltPipelinesReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: EtlEltPipelinesReadinessState.Deferred);
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository(metadata);
        var handler = new EvaluateEtlEltPipelinesReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEtlEltPipelinesReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EtlEltPipelinesReadinessState.Deferred, response.Data!.EtlEltPipelinesReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEtlEltPipelinesReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(pipelineDefinitionCatalogBoundaryState: EtlEltPipelinesReadinessState.Ready),
            ValidRequest(extractIntakeBoundaryState: EtlEltPipelinesReadinessState.Ready),
            ValidRequest(transformScopeBoundaryState: EtlEltPipelinesReadinessState.Ready),
            ValidRequest(loadControlBoundaryState: EtlEltPipelinesReadinessState.Ready),
            ValidRequest(orchestrationReviewBoundaryState: EtlEltPipelinesReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: EtlEltPipelinesReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateEtlEltPipelinesReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEtlEltPipelinesReadinessMetadataRepository(metadata);
        var response = await new GetEtlEltPipelinesAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEtlEltPipelinesAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(EtlEltPipelinesGuard.AuditReadPermission, PermissionFor(nameof(EtlEltPipelinesController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryEtlEltPipelinesReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateEtlEltPipelinesReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("etl-elt-pipelines pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEtlEltPipelinesReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateEtlEltPipelinesReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(EtlEltPipelinesReadinessCreateRequest),
            typeof(EtlEltPipelinesReadinessDto),
            typeof(EtlEltPipelinesReadinessMetadata)
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
        Assert.Null(typeof(EtlEltPipelinesReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(EtlEltPipelinesGuard.ReadPermission, PermissionFor(nameof(EtlEltPipelinesController.GetAll)));
        Assert.Equal(EtlEltPipelinesGuard.ReadPermission, PermissionFor(nameof(EtlEltPipelinesController.GetById)));
        Assert.Equal(EtlEltPipelinesGuard.ManagePermission, PermissionFor(nameof(EtlEltPipelinesController.Create)));
        Assert.Equal(EtlEltPipelinesGuard.EvaluatePermission, PermissionFor(nameof(EtlEltPipelinesController.Evaluate)));
        Assert.Equal(EtlEltPipelinesGuard.ManagePermission, PermissionFor(nameof(EtlEltPipelinesController.Delete)));
        Assert.Equal(EtlEltPipelinesGuard.AuditReadPermission, PermissionFor(nameof(EtlEltPipelinesController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_etl_elt_pipelines_readiness", MongoEtlEltPipelinesReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_etl_elt_pipelines_tenant_code_active", MongoEtlEltPipelinesReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_etl_elt_pipelines_tenant_state", MongoEtlEltPipelinesReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0064");
        var runtimeStrings = new[]
        {
            EtlEltPipelinesGuard.OwnerKey,
            EtlEltPipelinesGuard.ReadPermission,
            EtlEltPipelinesGuard.ManagePermission,
            EtlEltPipelinesGuard.EvaluatePermission,
            EtlEltPipelinesGuard.AuditReadPermission,
            MongoEtlEltPipelinesReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateEtlEltPipelinesReadinessHandler CreateHandler(
        IEtlEltPipelinesReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static EtlEltPipelinesReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "EtlEltPipelines readiness",
        EtlEltPipelinesReadinessState readinessState = EtlEltPipelinesReadinessState.Draft,
        string sourceContractVersion = "v1",
        EtlEltPipelinesReadinessState pipelineDefinitionCatalogBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
        EtlEltPipelinesReadinessState extractIntakeBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
        EtlEltPipelinesReadinessState transformScopeBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
        EtlEltPipelinesReadinessState loadControlBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
        EtlEltPipelinesReadinessState orchestrationReviewBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
        EtlEltPipelinesReadinessState automatedDecisionBoundaryState = EtlEltPipelinesReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            EtlEltPipelinesReadinessState = readinessState,
            PipelineDefinitionCatalogBoundaryState = pipelineDefinitionCatalogBoundaryState,
            ExtractIntakeBoundaryState = extractIntakeBoundaryState,
            TransformScopeBoundaryState = transformScopeBoundaryState,
            LoadControlBoundaryState = loadControlBoundaryState,
            OrchestrationReviewBoundaryState = orchestrationReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            LakehouseSourceDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            JobOrchestrationDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            DataContractRegistryDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            NotificationDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            StewardshipPreconditionState = EtlEltPipelinesReadinessState.Deferred,
            DataMinimizationState = EtlEltPipelinesReadinessState.Deferred,
            RetentionPolicyState = EtlEltPipelinesReadinessState.Deferred,
            MonitoringPolicyState = EtlEltPipelinesReadinessState.Deferred,
            DependencyStates = new Dictionary<string, EtlEltPipelinesReadinessState>
            {
                ["talentDataSource"] = EtlEltPipelinesReadinessState.Ready,
                ["consentPolicy"] = EtlEltPipelinesReadinessState.Ready,
                ["pipelineDefinitionCatalog"] = EtlEltPipelinesReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            EtlEltPipelinesReadinessVersion = 1
        };

    private static EtlEltPipelinesReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        EtlEltPipelinesReadinessState stewardshipPreconditionState = EtlEltPipelinesReadinessState.Deferred,
        EtlEltPipelinesReadinessState dataMinimizationState = EtlEltPipelinesReadinessState.Deferred,
        EtlEltPipelinesReadinessState retentionPolicyState = EtlEltPipelinesReadinessState.Deferred,
        EtlEltPipelinesReadinessState monitoringPolicyState = EtlEltPipelinesReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "EtlEltPipelines readiness",
            EtlEltPipelinesReadinessState = EtlEltPipelinesReadinessState.Draft,
            PipelineDefinitionCatalogBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            ExtractIntakeBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            TransformScopeBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            LoadControlBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            OrchestrationReviewBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = EtlEltPipelinesReadinessState.NotRequired,
            LakehouseSourceDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            JobOrchestrationDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            DataContractRegistryDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            NotificationDependencyState = EtlEltPipelinesReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            MonitoringPolicyState = monitoringPolicyState,
            DependencyStates = new Dictionary<string, EtlEltPipelinesReadinessState>
            {
                ["talentDataSource"] = EtlEltPipelinesReadinessState.Ready
            },
            SourceContractVersion = "v1",
            EtlEltPipelinesReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, EtlEltPipelinesReadinessMetadata> RepositoryItems(
        InMemoryEtlEltPipelinesReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryEtlEltPipelinesReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EtlEltPipelinesReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(EtlEltPipelinesController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryEtlEltPipelinesReadinessMetadataRepository : IEtlEltPipelinesReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, EtlEltPipelinesReadinessMetadata> _items;

        public InMemoryEtlEltPipelinesReadinessMetadataRepository(params EtlEltPipelinesReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EtlEltPipelinesReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtlEltPipelinesReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<EtlEltPipelinesReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EtlEltPipelinesReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
