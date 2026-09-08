using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Handlers;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class ScorecardsDashboardsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateScorecardsDashboardsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetScorecardsDashboardsReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetScorecardsDashboardsReadinessListQuery(), CancellationToken.None);
        var get = await new GetScorecardsDashboardsReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetScorecardsDashboardsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("ScorecardsDashboards readiness", get.Data.DisplayName);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.ScorecardCatalogBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.WidgetBindingIntakeBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.LayoutScopeBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.PublicationControlBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.DashboardReviewBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.MetricSemanticRegistrySourceDependencyState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.DataWarehouseSourceDependencyState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(ScorecardsDashboardsReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetScorecardsDashboardsReadinessByIdHandler(
            new InMemoryScorecardsDashboardsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetScorecardsDashboardsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryScorecardsDashboardsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateScorecardsDashboardsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateScorecardsDashboardsReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateScorecardsDashboardsReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository(metadata);
        var handler = new DeleteScorecardsDashboardsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteScorecardsDashboardsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(ScorecardsDashboardsReadinessState.Archived, stored.ScorecardsDashboardsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateScorecardsDashboardsReadinessCommand(ValidRequest(readinessState: ScorecardsDashboardsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(ScorecardsDashboardsReadinessState.Deferred, stored.ScorecardsDashboardsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: ScorecardsDashboardsReadinessState.Ready,
            dataMinimizationState: ScorecardsDashboardsReadinessState.Ready,
            retentionPolicyState: ScorecardsDashboardsReadinessState.Ready,
            publicationPolicyState: ScorecardsDashboardsReadinessState.Ready);
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository(metadata);
        var handler = new EvaluateScorecardsDashboardsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateScorecardsDashboardsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ScorecardsDashboardsReadinessState.Ready, response.Data!.ScorecardsDashboardsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: ScorecardsDashboardsReadinessState.Deferred);
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository(metadata);
        var handler = new EvaluateScorecardsDashboardsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateScorecardsDashboardsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ScorecardsDashboardsReadinessState.Deferred, response.Data!.ScorecardsDashboardsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryScorecardsDashboardsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(scorecardCatalogBoundaryState: ScorecardsDashboardsReadinessState.Ready),
            ValidRequest(widgetBindingIntakeBoundaryState: ScorecardsDashboardsReadinessState.Ready),
            ValidRequest(layoutScopeBoundaryState: ScorecardsDashboardsReadinessState.Ready),
            ValidRequest(publicationControlBoundaryState: ScorecardsDashboardsReadinessState.Ready),
            ValidRequest(dashboardReviewBoundaryState: ScorecardsDashboardsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: ScorecardsDashboardsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateScorecardsDashboardsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryScorecardsDashboardsReadinessMetadataRepository(metadata);
        var response = await new GetScorecardsDashboardsAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetScorecardsDashboardsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(ScorecardsDashboardsGuard.AuditReadPermission, PermissionFor(nameof(ScorecardsDashboardsController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryScorecardsDashboardsReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateScorecardsDashboardsReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("scorecards-dashboards pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryScorecardsDashboardsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateScorecardsDashboardsReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(ScorecardsDashboardsReadinessCreateRequest),
            typeof(ScorecardsDashboardsReadinessDto),
            typeof(ScorecardsDashboardsReadinessMetadata)
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
        Assert.Null(typeof(ScorecardsDashboardsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(ScorecardsDashboardsGuard.ReadPermission, PermissionFor(nameof(ScorecardsDashboardsController.GetAll)));
        Assert.Equal(ScorecardsDashboardsGuard.ReadPermission, PermissionFor(nameof(ScorecardsDashboardsController.GetById)));
        Assert.Equal(ScorecardsDashboardsGuard.ManagePermission, PermissionFor(nameof(ScorecardsDashboardsController.Create)));
        Assert.Equal(ScorecardsDashboardsGuard.EvaluatePermission, PermissionFor(nameof(ScorecardsDashboardsController.Evaluate)));
        Assert.Equal(ScorecardsDashboardsGuard.ManagePermission, PermissionFor(nameof(ScorecardsDashboardsController.Delete)));
        Assert.Equal(ScorecardsDashboardsGuard.AuditReadPermission, PermissionFor(nameof(ScorecardsDashboardsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_scorecards_dashboards_readiness", MongoScorecardsDashboardsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_scorecards_dashboards_tenant_code_active", MongoScorecardsDashboardsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_scorecards_dashboards_tenant_state", MongoScorecardsDashboardsReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0061");
        var runtimeStrings = new[]
        {
            ScorecardsDashboardsGuard.OwnerKey,
            ScorecardsDashboardsGuard.ReadPermission,
            ScorecardsDashboardsGuard.ManagePermission,
            ScorecardsDashboardsGuard.EvaluatePermission,
            ScorecardsDashboardsGuard.AuditReadPermission,
            MongoScorecardsDashboardsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateScorecardsDashboardsReadinessHandler CreateHandler(
        IScorecardsDashboardsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static ScorecardsDashboardsReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "ScorecardsDashboards readiness",
        ScorecardsDashboardsReadinessState readinessState = ScorecardsDashboardsReadinessState.Draft,
        string sourceContractVersion = "v1",
        ScorecardsDashboardsReadinessState scorecardCatalogBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
        ScorecardsDashboardsReadinessState widgetBindingIntakeBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
        ScorecardsDashboardsReadinessState layoutScopeBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
        ScorecardsDashboardsReadinessState publicationControlBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
        ScorecardsDashboardsReadinessState dashboardReviewBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
        ScorecardsDashboardsReadinessState automatedDecisionBoundaryState = ScorecardsDashboardsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            ScorecardsDashboardsReadinessState = readinessState,
            ScorecardCatalogBoundaryState = scorecardCatalogBoundaryState,
            WidgetBindingIntakeBoundaryState = widgetBindingIntakeBoundaryState,
            LayoutScopeBoundaryState = layoutScopeBoundaryState,
            PublicationControlBoundaryState = publicationControlBoundaryState,
            DashboardReviewBoundaryState = dashboardReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            DataWarehouseSourceDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            DataContractRegistryDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            NotificationDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            StewardshipPreconditionState = ScorecardsDashboardsReadinessState.Deferred,
            DataMinimizationState = ScorecardsDashboardsReadinessState.Deferred,
            RetentionPolicyState = ScorecardsDashboardsReadinessState.Deferred,
            PublicationPolicyState = ScorecardsDashboardsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, ScorecardsDashboardsReadinessState>
            {
                ["talentDataSource"] = ScorecardsDashboardsReadinessState.Ready,
                ["consentPolicy"] = ScorecardsDashboardsReadinessState.Ready,
                ["scorecardCatalog"] = ScorecardsDashboardsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            ScorecardsDashboardsReadinessVersion = 1
        };

    private static ScorecardsDashboardsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        ScorecardsDashboardsReadinessState stewardshipPreconditionState = ScorecardsDashboardsReadinessState.Deferred,
        ScorecardsDashboardsReadinessState dataMinimizationState = ScorecardsDashboardsReadinessState.Deferred,
        ScorecardsDashboardsReadinessState retentionPolicyState = ScorecardsDashboardsReadinessState.Deferred,
        ScorecardsDashboardsReadinessState publicationPolicyState = ScorecardsDashboardsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "ScorecardsDashboards readiness",
            ScorecardsDashboardsReadinessState = ScorecardsDashboardsReadinessState.Draft,
            ScorecardCatalogBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            WidgetBindingIntakeBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            LayoutScopeBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            PublicationControlBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            DashboardReviewBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = ScorecardsDashboardsReadinessState.NotRequired,
            MetricSemanticRegistrySourceDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            DataWarehouseSourceDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            DataContractRegistryDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            NotificationDependencyState = ScorecardsDashboardsReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, ScorecardsDashboardsReadinessState>
            {
                ["talentDataSource"] = ScorecardsDashboardsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            ScorecardsDashboardsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, ScorecardsDashboardsReadinessMetadata> RepositoryItems(
        InMemoryScorecardsDashboardsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryScorecardsDashboardsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, ScorecardsDashboardsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(ScorecardsDashboardsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryScorecardsDashboardsReadinessMetadataRepository : IScorecardsDashboardsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, ScorecardsDashboardsReadinessMetadata> _items;

        public InMemoryScorecardsDashboardsReadinessMetadataRepository(params ScorecardsDashboardsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<ScorecardsDashboardsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ScorecardsDashboardsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<ScorecardsDashboardsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ScorecardsDashboardsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
