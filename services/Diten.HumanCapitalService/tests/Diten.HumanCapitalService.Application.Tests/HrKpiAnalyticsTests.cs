using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Handlers;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class HrKpiAnalyticsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHrKpiAnalyticsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHrKpiAnalyticsReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrKpiAnalyticsReadinessListQuery(), CancellationToken.None);
        var get = await new GetHrKpiAnalyticsReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrKpiAnalyticsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HrKpiAnalytics readiness", get.Data.DisplayName);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.KpiCatalogBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.MetricDefinitionBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.DashboardBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.AnalyticsQueryBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.DataExportBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.AnalyticsPlatformDependencyState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.DataSourceDependencyState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HrKpiAnalyticsReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHrKpiAnalyticsReadinessByIdHandler(
            new InMemoryHrKpiAnalyticsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetHrKpiAnalyticsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHrKpiAnalyticsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHrKpiAnalyticsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHrKpiAnalyticsReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHrKpiAnalyticsReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository(metadata);
        var handler = new DeleteHrKpiAnalyticsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteHrKpiAnalyticsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HrKpiAnalyticsReadinessState.Archived, stored.HrKpiAnalyticsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHrKpiAnalyticsReadinessCommand(ValidRequest(readinessState: HrKpiAnalyticsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrKpiAnalyticsReadinessState.Deferred, stored.HrKpiAnalyticsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HrKpiAnalyticsReadinessState.Ready,
            dataMinimizationState: HrKpiAnalyticsReadinessState.Ready,
            retentionPolicyState: HrKpiAnalyticsReadinessState.Ready,
            evidencePolicyState: HrKpiAnalyticsReadinessState.Ready);
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrKpiAnalyticsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrKpiAnalyticsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrKpiAnalyticsReadinessState.Ready, response.Data!.HrKpiAnalyticsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HrKpiAnalyticsReadinessState.Deferred);
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrKpiAnalyticsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrKpiAnalyticsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrKpiAnalyticsReadinessState.Deferred, response.Data!.HrKpiAnalyticsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrKpiAnalyticsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(kpiCatalogBoundaryState: HrKpiAnalyticsReadinessState.Ready),
            ValidRequest(metricDefinitionBoundaryState: HrKpiAnalyticsReadinessState.Ready),
            ValidRequest(dashboardBoundaryState: HrKpiAnalyticsReadinessState.Ready),
            ValidRequest(analyticsQueryBoundaryState: HrKpiAnalyticsReadinessState.Ready),
            ValidRequest(dataExportBoundaryState: HrKpiAnalyticsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HrKpiAnalyticsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHrKpiAnalyticsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrKpiAnalyticsReadinessMetadataRepository(metadata);
        var response = await new GetHrKpiAnalyticsAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrKpiAnalyticsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HrKpiAnalyticsGuard.AuditReadPermission, PermissionFor(nameof(HrKpiAnalyticsController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHrKpiAnalyticsReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHrKpiAnalyticsReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("hr-kpi-analytics pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrKpiAnalyticsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHrKpiAnalyticsReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HrKpiAnalyticsReadinessCreateRequest),
            typeof(HrKpiAnalyticsReadinessDto),
            typeof(HrKpiAnalyticsReadinessMetadata)
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
        Assert.Null(typeof(HrKpiAnalyticsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HrKpiAnalyticsGuard.ReadPermission, PermissionFor(nameof(HrKpiAnalyticsController.GetAll)));
        Assert.Equal(HrKpiAnalyticsGuard.ReadPermission, PermissionFor(nameof(HrKpiAnalyticsController.GetById)));
        Assert.Equal(HrKpiAnalyticsGuard.ManagePermission, PermissionFor(nameof(HrKpiAnalyticsController.Create)));
        Assert.Equal(HrKpiAnalyticsGuard.EvaluatePermission, PermissionFor(nameof(HrKpiAnalyticsController.Evaluate)));
        Assert.Equal(HrKpiAnalyticsGuard.ManagePermission, PermissionFor(nameof(HrKpiAnalyticsController.Delete)));
        Assert.Equal(HrKpiAnalyticsGuard.AuditReadPermission, PermissionFor(nameof(HrKpiAnalyticsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_hr_kpi_analytics_readiness", MongoHrKpiAnalyticsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_hr_kpi_analytics_tenant_code_active", MongoHrKpiAnalyticsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_hr_kpi_analytics_tenant_state", MongoHrKpiAnalyticsReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0034");
        var legacy = string.Join("-", "MOD", "0312");
        var runtimeStrings = new[]
        {
            HrKpiAnalyticsGuard.OwnerKey,
            HrKpiAnalyticsGuard.ReadPermission,
            HrKpiAnalyticsGuard.ManagePermission,
            HrKpiAnalyticsGuard.EvaluatePermission,
            HrKpiAnalyticsGuard.AuditReadPermission,
            MongoHrKpiAnalyticsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHrKpiAnalyticsReadinessHandler CreateHandler(
        IHrKpiAnalyticsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static HrKpiAnalyticsReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HrKpiAnalytics readiness",
        HrKpiAnalyticsReadinessState readinessState = HrKpiAnalyticsReadinessState.Draft,
        string sourceContractVersion = "v1",
        HrKpiAnalyticsReadinessState kpiCatalogBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
        HrKpiAnalyticsReadinessState metricDefinitionBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
        HrKpiAnalyticsReadinessState dashboardBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
        HrKpiAnalyticsReadinessState analyticsQueryBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
        HrKpiAnalyticsReadinessState dataExportBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
        HrKpiAnalyticsReadinessState automatedDecisionBoundaryState = HrKpiAnalyticsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HrKpiAnalyticsReadinessState = readinessState,
            KpiCatalogBoundaryState = kpiCatalogBoundaryState,
            MetricDefinitionBoundaryState = metricDefinitionBoundaryState,
            DashboardBoundaryState = dashboardBoundaryState,
            AnalyticsQueryBoundaryState = analyticsQueryBoundaryState,
            DataExportBoundaryState = dataExportBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            AnalyticsPlatformDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            DataSourceDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            DocumentDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            NotificationDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            ConsentPreconditionState = HrKpiAnalyticsReadinessState.Deferred,
            DataMinimizationState = HrKpiAnalyticsReadinessState.Deferred,
            RetentionPolicyState = HrKpiAnalyticsReadinessState.Deferred,
            EvidencePolicyState = HrKpiAnalyticsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HrKpiAnalyticsReadinessState>
            {
                ["analyticsPlatform"] = HrKpiAnalyticsReadinessState.Ready,
                ["dataSource"] = HrKpiAnalyticsReadinessState.Ready,
                ["kpiCatalog"] = HrKpiAnalyticsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HrKpiAnalyticsReadinessVersion = 1
        };

    private static HrKpiAnalyticsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HrKpiAnalyticsReadinessState consentPreconditionState = HrKpiAnalyticsReadinessState.Deferred,
        HrKpiAnalyticsReadinessState dataMinimizationState = HrKpiAnalyticsReadinessState.Deferred,
        HrKpiAnalyticsReadinessState retentionPolicyState = HrKpiAnalyticsReadinessState.Deferred,
        HrKpiAnalyticsReadinessState evidencePolicyState = HrKpiAnalyticsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "HrKpiAnalytics readiness",
            HrKpiAnalyticsReadinessState = HrKpiAnalyticsReadinessState.Draft,
            KpiCatalogBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            MetricDefinitionBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            DashboardBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            AnalyticsQueryBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            DataExportBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HrKpiAnalyticsReadinessState.NotRequired,
            AnalyticsPlatformDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            DataSourceDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            DocumentDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            NotificationDependencyState = HrKpiAnalyticsReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HrKpiAnalyticsReadinessState>
            {
                ["analyticsPlatform"] = HrKpiAnalyticsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HrKpiAnalyticsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HrKpiAnalyticsReadinessMetadata> RepositoryItems(
        InMemoryHrKpiAnalyticsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHrKpiAnalyticsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HrKpiAnalyticsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HrKpiAnalyticsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryHrKpiAnalyticsReadinessMetadataRepository : IHrKpiAnalyticsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HrKpiAnalyticsReadinessMetadata> _items;

        public InMemoryHrKpiAnalyticsReadinessMetadataRepository(params HrKpiAnalyticsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HrKpiAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HrKpiAnalyticsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<HrKpiAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
