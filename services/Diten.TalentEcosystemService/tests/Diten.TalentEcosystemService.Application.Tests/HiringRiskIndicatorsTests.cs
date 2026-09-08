using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Handlers;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class HiringRiskIndicatorsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHiringRiskIndicatorsReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHiringRiskIndicatorsReadinessListQuery(), CancellationToken.None);
        var get = await new GetHiringRiskIndicatorsReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHiringRiskIndicatorsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HiringRiskIndicators readiness", get.Data.DisplayName);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.RiskIndicatorCatalogBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.RiskSignalIntakeBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.RiskAssessmentBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.MitigationTrackingBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.IndicatorReviewBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HiringRiskIndicatorsReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHiringRiskIndicatorsReadinessByIdHandler(
            new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetHiringRiskIndicatorsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(metadata);
        var handler = new DeleteHiringRiskIndicatorsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteHiringRiskIndicatorsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HiringRiskIndicatorsReadinessState.Archived, stored.HiringRiskIndicatorsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest(readinessState: HiringRiskIndicatorsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HiringRiskIndicatorsReadinessState.Deferred, stored.HiringRiskIndicatorsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HiringRiskIndicatorsReadinessState.Ready,
            dataMinimizationState: HiringRiskIndicatorsReadinessState.Ready,
            retentionPolicyState: HiringRiskIndicatorsReadinessState.Ready,
            evidencePolicyState: HiringRiskIndicatorsReadinessState.Ready);
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(metadata);
        var handler = new EvaluateHiringRiskIndicatorsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHiringRiskIndicatorsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HiringRiskIndicatorsReadinessState.Ready, response.Data!.HiringRiskIndicatorsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HiringRiskIndicatorsReadinessState.Deferred);
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(metadata);
        var handler = new EvaluateHiringRiskIndicatorsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHiringRiskIndicatorsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HiringRiskIndicatorsReadinessState.Deferred, response.Data!.HiringRiskIndicatorsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(riskIndicatorCatalogBoundaryState: HiringRiskIndicatorsReadinessState.Ready),
            ValidRequest(riskSignalIntakeBoundaryState: HiringRiskIndicatorsReadinessState.Ready),
            ValidRequest(riskAssessmentBoundaryState: HiringRiskIndicatorsReadinessState.Ready),
            ValidRequest(mitigationTrackingBoundaryState: HiringRiskIndicatorsReadinessState.Ready),
            ValidRequest(indicatorReviewBoundaryState: HiringRiskIndicatorsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HiringRiskIndicatorsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHiringRiskIndicatorsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(metadata);
        var response = await new GetHiringRiskIndicatorsAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHiringRiskIndicatorsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HiringRiskIndicatorsGuard.AuditReadPermission, PermissionFor(nameof(HiringRiskIndicatorsController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("hiring-risk-indicators pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHiringRiskIndicatorsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHiringRiskIndicatorsReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HiringRiskIndicatorsReadinessCreateRequest),
            typeof(HiringRiskIndicatorsReadinessDto),
            typeof(HiringRiskIndicatorsReadinessMetadata)
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
        Assert.Null(typeof(HiringRiskIndicatorsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HiringRiskIndicatorsGuard.ReadPermission, PermissionFor(nameof(HiringRiskIndicatorsController.GetAll)));
        Assert.Equal(HiringRiskIndicatorsGuard.ReadPermission, PermissionFor(nameof(HiringRiskIndicatorsController.GetById)));
        Assert.Equal(HiringRiskIndicatorsGuard.ManagePermission, PermissionFor(nameof(HiringRiskIndicatorsController.Create)));
        Assert.Equal(HiringRiskIndicatorsGuard.EvaluatePermission, PermissionFor(nameof(HiringRiskIndicatorsController.Evaluate)));
        Assert.Equal(HiringRiskIndicatorsGuard.ManagePermission, PermissionFor(nameof(HiringRiskIndicatorsController.Delete)));
        Assert.Equal(HiringRiskIndicatorsGuard.AuditReadPermission, PermissionFor(nameof(HiringRiskIndicatorsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_hiring_risk_indicators_readiness", MongoHiringRiskIndicatorsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_hiring_risk_indicators_tenant_code_active", MongoHiringRiskIndicatorsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_hiring_risk_indicators_tenant_state", MongoHiringRiskIndicatorsReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0042");
        var legacy = string.Join("-", "MOD", "0332");
        var runtimeStrings = new[]
        {
            HiringRiskIndicatorsGuard.OwnerKey,
            HiringRiskIndicatorsGuard.ReadPermission,
            HiringRiskIndicatorsGuard.ManagePermission,
            HiringRiskIndicatorsGuard.EvaluatePermission,
            HiringRiskIndicatorsGuard.AuditReadPermission,
            MongoHiringRiskIndicatorsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHiringRiskIndicatorsReadinessHandler CreateHandler(
        IHiringRiskIndicatorsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static HiringRiskIndicatorsReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HiringRiskIndicators readiness",
        HiringRiskIndicatorsReadinessState readinessState = HiringRiskIndicatorsReadinessState.Draft,
        string sourceContractVersion = "v1",
        HiringRiskIndicatorsReadinessState riskIndicatorCatalogBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
        HiringRiskIndicatorsReadinessState riskSignalIntakeBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
        HiringRiskIndicatorsReadinessState riskAssessmentBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
        HiringRiskIndicatorsReadinessState mitigationTrackingBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
        HiringRiskIndicatorsReadinessState indicatorReviewBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
        HiringRiskIndicatorsReadinessState automatedDecisionBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HiringRiskIndicatorsReadinessState = readinessState,
            RiskIndicatorCatalogBoundaryState = riskIndicatorCatalogBoundaryState,
            RiskSignalIntakeBoundaryState = riskSignalIntakeBoundaryState,
            RiskAssessmentBoundaryState = riskAssessmentBoundaryState,
            MitigationTrackingBoundaryState = mitigationTrackingBoundaryState,
            IndicatorReviewBoundaryState = indicatorReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            ConsentPolicyDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            DocumentDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            NotificationDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            ConsentPreconditionState = HiringRiskIndicatorsReadinessState.Deferred,
            DataMinimizationState = HiringRiskIndicatorsReadinessState.Deferred,
            RetentionPolicyState = HiringRiskIndicatorsReadinessState.Deferred,
            EvidencePolicyState = HiringRiskIndicatorsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HiringRiskIndicatorsReadinessState>
            {
                ["talentDataSource"] = HiringRiskIndicatorsReadinessState.Ready,
                ["consentPolicy"] = HiringRiskIndicatorsReadinessState.Ready,
                ["riskIndicatorCatalog"] = HiringRiskIndicatorsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HiringRiskIndicatorsReadinessVersion = 1
        };

    private static HiringRiskIndicatorsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HiringRiskIndicatorsReadinessState consentPreconditionState = HiringRiskIndicatorsReadinessState.Deferred,
        HiringRiskIndicatorsReadinessState dataMinimizationState = HiringRiskIndicatorsReadinessState.Deferred,
        HiringRiskIndicatorsReadinessState retentionPolicyState = HiringRiskIndicatorsReadinessState.Deferred,
        HiringRiskIndicatorsReadinessState evidencePolicyState = HiringRiskIndicatorsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "HiringRiskIndicators readiness",
            HiringRiskIndicatorsReadinessState = HiringRiskIndicatorsReadinessState.Draft,
            RiskIndicatorCatalogBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            RiskSignalIntakeBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            RiskAssessmentBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            MitigationTrackingBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            IndicatorReviewBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HiringRiskIndicatorsReadinessState.NotRequired,
            TalentDataSourceDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            ConsentPolicyDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            DocumentDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            NotificationDependencyState = HiringRiskIndicatorsReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HiringRiskIndicatorsReadinessState>
            {
                ["talentDataSource"] = HiringRiskIndicatorsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HiringRiskIndicatorsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HiringRiskIndicatorsReadinessMetadata> RepositoryItems(
        InMemoryHiringRiskIndicatorsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHiringRiskIndicatorsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HiringRiskIndicatorsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HiringRiskIndicatorsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryHiringRiskIndicatorsReadinessMetadataRepository : IHiringRiskIndicatorsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HiringRiskIndicatorsReadinessMetadata> _items;

        public InMemoryHiringRiskIndicatorsReadinessMetadataRepository(params HiringRiskIndicatorsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HiringRiskIndicatorsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HiringRiskIndicatorsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<HiringRiskIndicatorsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
