using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Handlers;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class KpiCatalogTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateKpiCatalogReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetKpiCatalogReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetKpiCatalogReadinessListQuery(), CancellationToken.None);
        var get = await new GetKpiCatalogReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetKpiCatalogReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("KpiCatalog readiness", get.Data.DisplayName);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.KpiIdentityCatalogBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.DefinitionBindingIntakeBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.OwnershipScopeBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.PublicationControlBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.CatalogReviewBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.MetricSemanticRegistrySourceDependencyState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.DataDictionaryDependencyState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(KpiCatalogReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetKpiCatalogReadinessByIdHandler(
            new InMemoryKpiCatalogReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetKpiCatalogReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryKpiCatalogReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateKpiCatalogReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateKpiCatalogReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateKpiCatalogReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository(metadata);
        var handler = new DeleteKpiCatalogReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteKpiCatalogReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(KpiCatalogReadinessState.Archived, stored.KpiCatalogReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateKpiCatalogReadinessCommand(ValidRequest(readinessState: KpiCatalogReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(KpiCatalogReadinessState.Deferred, stored.KpiCatalogReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: KpiCatalogReadinessState.Ready,
            dataMinimizationState: KpiCatalogReadinessState.Ready,
            retentionPolicyState: KpiCatalogReadinessState.Ready,
            publicationPolicyState: KpiCatalogReadinessState.Ready);
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository(metadata);
        var handler = new EvaluateKpiCatalogReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateKpiCatalogReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(KpiCatalogReadinessState.Ready, response.Data!.KpiCatalogReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: KpiCatalogReadinessState.Deferred);
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository(metadata);
        var handler = new EvaluateKpiCatalogReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateKpiCatalogReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(KpiCatalogReadinessState.Deferred, response.Data!.KpiCatalogReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryKpiCatalogReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(kpiIdentityCatalogBoundaryState: KpiCatalogReadinessState.Ready),
            ValidRequest(definitionBindingIntakeBoundaryState: KpiCatalogReadinessState.Ready),
            ValidRequest(ownershipScopeBoundaryState: KpiCatalogReadinessState.Ready),
            ValidRequest(publicationControlBoundaryState: KpiCatalogReadinessState.Ready),
            ValidRequest(catalogReviewBoundaryState: KpiCatalogReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: KpiCatalogReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateKpiCatalogReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryKpiCatalogReadinessMetadataRepository(metadata);
        var response = await new GetKpiCatalogAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetKpiCatalogAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(KpiCatalogGuard.AuditReadPermission, PermissionFor(nameof(KpiCatalogController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryKpiCatalogReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateKpiCatalogReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("kpi-catalog pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryKpiCatalogReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateKpiCatalogReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(KpiCatalogReadinessCreateRequest),
            typeof(KpiCatalogReadinessDto),
            typeof(KpiCatalogReadinessMetadata)
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
        Assert.Null(typeof(KpiCatalogReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(KpiCatalogGuard.ReadPermission, PermissionFor(nameof(KpiCatalogController.GetAll)));
        Assert.Equal(KpiCatalogGuard.ReadPermission, PermissionFor(nameof(KpiCatalogController.GetById)));
        Assert.Equal(KpiCatalogGuard.ManagePermission, PermissionFor(nameof(KpiCatalogController.Create)));
        Assert.Equal(KpiCatalogGuard.EvaluatePermission, PermissionFor(nameof(KpiCatalogController.Evaluate)));
        Assert.Equal(KpiCatalogGuard.ManagePermission, PermissionFor(nameof(KpiCatalogController.Delete)));
        Assert.Equal(KpiCatalogGuard.AuditReadPermission, PermissionFor(nameof(KpiCatalogController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_kpi_catalog_readiness", MongoKpiCatalogReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_kpi_catalog_tenant_code_active", MongoKpiCatalogReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_kpi_catalog_tenant_state", MongoKpiCatalogReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0059");
        var runtimeStrings = new[]
        {
            KpiCatalogGuard.OwnerKey,
            KpiCatalogGuard.ReadPermission,
            KpiCatalogGuard.ManagePermission,
            KpiCatalogGuard.EvaluatePermission,
            KpiCatalogGuard.AuditReadPermission,
            MongoKpiCatalogReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateKpiCatalogReadinessHandler CreateHandler(
        IKpiCatalogReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static KpiCatalogReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "KpiCatalog readiness",
        KpiCatalogReadinessState readinessState = KpiCatalogReadinessState.Draft,
        string sourceContractVersion = "v1",
        KpiCatalogReadinessState kpiIdentityCatalogBoundaryState = KpiCatalogReadinessState.NotRequired,
        KpiCatalogReadinessState definitionBindingIntakeBoundaryState = KpiCatalogReadinessState.NotRequired,
        KpiCatalogReadinessState ownershipScopeBoundaryState = KpiCatalogReadinessState.NotRequired,
        KpiCatalogReadinessState publicationControlBoundaryState = KpiCatalogReadinessState.NotRequired,
        KpiCatalogReadinessState catalogReviewBoundaryState = KpiCatalogReadinessState.NotRequired,
        KpiCatalogReadinessState automatedDecisionBoundaryState = KpiCatalogReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            KpiCatalogReadinessState = readinessState,
            KpiIdentityCatalogBoundaryState = kpiIdentityCatalogBoundaryState,
            DefinitionBindingIntakeBoundaryState = definitionBindingIntakeBoundaryState,
            OwnershipScopeBoundaryState = ownershipScopeBoundaryState,
            PublicationControlBoundaryState = publicationControlBoundaryState,
            CatalogReviewBoundaryState = catalogReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = KpiCatalogReadinessState.NotRequired,
            DataDictionaryDependencyState = KpiCatalogReadinessState.NotRequired,
            DataContractRegistryDependencyState = KpiCatalogReadinessState.NotRequired,
            NotificationDependencyState = KpiCatalogReadinessState.NotRequired,
            StewardshipPreconditionState = KpiCatalogReadinessState.Deferred,
            DataMinimizationState = KpiCatalogReadinessState.Deferred,
            RetentionPolicyState = KpiCatalogReadinessState.Deferred,
            PublicationPolicyState = KpiCatalogReadinessState.Deferred,
            DependencyStates = new Dictionary<string, KpiCatalogReadinessState>
            {
                ["talentDataSource"] = KpiCatalogReadinessState.Ready,
                ["consentPolicy"] = KpiCatalogReadinessState.Ready,
                ["kpiIdentityCatalog"] = KpiCatalogReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            KpiCatalogReadinessVersion = 1
        };

    private static KpiCatalogReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        KpiCatalogReadinessState stewardshipPreconditionState = KpiCatalogReadinessState.Deferred,
        KpiCatalogReadinessState dataMinimizationState = KpiCatalogReadinessState.Deferred,
        KpiCatalogReadinessState retentionPolicyState = KpiCatalogReadinessState.Deferred,
        KpiCatalogReadinessState publicationPolicyState = KpiCatalogReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "KpiCatalog readiness",
            KpiCatalogReadinessState = KpiCatalogReadinessState.Draft,
            KpiIdentityCatalogBoundaryState = KpiCatalogReadinessState.NotRequired,
            DefinitionBindingIntakeBoundaryState = KpiCatalogReadinessState.NotRequired,
            OwnershipScopeBoundaryState = KpiCatalogReadinessState.NotRequired,
            PublicationControlBoundaryState = KpiCatalogReadinessState.NotRequired,
            CatalogReviewBoundaryState = KpiCatalogReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = KpiCatalogReadinessState.NotRequired,
            MetricSemanticRegistrySourceDependencyState = KpiCatalogReadinessState.NotRequired,
            DataDictionaryDependencyState = KpiCatalogReadinessState.NotRequired,
            DataContractRegistryDependencyState = KpiCatalogReadinessState.NotRequired,
            NotificationDependencyState = KpiCatalogReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, KpiCatalogReadinessState>
            {
                ["talentDataSource"] = KpiCatalogReadinessState.Ready
            },
            SourceContractVersion = "v1",
            KpiCatalogReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, KpiCatalogReadinessMetadata> RepositoryItems(
        InMemoryKpiCatalogReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryKpiCatalogReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, KpiCatalogReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(KpiCatalogController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryKpiCatalogReadinessMetadataRepository : IKpiCatalogReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, KpiCatalogReadinessMetadata> _items;

        public InMemoryKpiCatalogReadinessMetadataRepository(params KpiCatalogReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<KpiCatalogReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KpiCatalogReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<KpiCatalogReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(KpiCatalogReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
