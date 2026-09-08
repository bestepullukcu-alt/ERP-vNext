using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Commands;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Handlers;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class EarlyWarningSignalsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateEarlyWarningSignalsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetEarlyWarningSignalsReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEarlyWarningSignalsReadinessListQuery(), CancellationToken.None);
        var get = await new GetEarlyWarningSignalsReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEarlyWarningSignalsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("EarlyWarningSignals readiness", get.Data.DisplayName);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.SignalCatalogBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.PatternDetectionBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.CrossCompanyCorrelationBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.AlertRoutingBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.SignalReviewBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.RiskIndicatorSourceDependencyState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(EarlyWarningSignalsReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetEarlyWarningSignalsReadinessByIdHandler(
            new InMemoryEarlyWarningSignalsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetEarlyWarningSignalsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryEarlyWarningSignalsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateEarlyWarningSignalsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateEarlyWarningSignalsReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateEarlyWarningSignalsReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository(metadata);
        var handler = new DeleteEarlyWarningSignalsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteEarlyWarningSignalsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(EarlyWarningSignalsReadinessState.Archived, stored.EarlyWarningSignalsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateEarlyWarningSignalsReadinessCommand(ValidRequest(readinessState: EarlyWarningSignalsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(EarlyWarningSignalsReadinessState.Deferred, stored.EarlyWarningSignalsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: EarlyWarningSignalsReadinessState.Ready,
            dataMinimizationState: EarlyWarningSignalsReadinessState.Ready,
            retentionPolicyState: EarlyWarningSignalsReadinessState.Ready,
            evidencePolicyState: EarlyWarningSignalsReadinessState.Ready);
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository(metadata);
        var handler = new EvaluateEarlyWarningSignalsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEarlyWarningSignalsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EarlyWarningSignalsReadinessState.Ready, response.Data!.EarlyWarningSignalsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: EarlyWarningSignalsReadinessState.Deferred);
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository(metadata);
        var handler = new EvaluateEarlyWarningSignalsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEarlyWarningSignalsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EarlyWarningSignalsReadinessState.Deferred, response.Data!.EarlyWarningSignalsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEarlyWarningSignalsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(signalCatalogBoundaryState: EarlyWarningSignalsReadinessState.Ready),
            ValidRequest(patternDetectionBoundaryState: EarlyWarningSignalsReadinessState.Ready),
            ValidRequest(crossCompanyCorrelationBoundaryState: EarlyWarningSignalsReadinessState.Ready),
            ValidRequest(alertRoutingBoundaryState: EarlyWarningSignalsReadinessState.Ready),
            ValidRequest(signalReviewBoundaryState: EarlyWarningSignalsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: EarlyWarningSignalsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateEarlyWarningSignalsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEarlyWarningSignalsReadinessMetadataRepository(metadata);
        var response = await new GetEarlyWarningSignalsAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEarlyWarningSignalsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(EarlyWarningSignalsGuard.AuditReadPermission, PermissionFor(nameof(EarlyWarningSignalsController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryEarlyWarningSignalsReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateEarlyWarningSignalsReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("early-warning-signals pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEarlyWarningSignalsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateEarlyWarningSignalsReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(EarlyWarningSignalsReadinessCreateRequest),
            typeof(EarlyWarningSignalsReadinessDto),
            typeof(EarlyWarningSignalsReadinessMetadata)
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
        Assert.Null(typeof(EarlyWarningSignalsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(EarlyWarningSignalsGuard.ReadPermission, PermissionFor(nameof(EarlyWarningSignalsController.GetAll)));
        Assert.Equal(EarlyWarningSignalsGuard.ReadPermission, PermissionFor(nameof(EarlyWarningSignalsController.GetById)));
        Assert.Equal(EarlyWarningSignalsGuard.ManagePermission, PermissionFor(nameof(EarlyWarningSignalsController.Create)));
        Assert.Equal(EarlyWarningSignalsGuard.EvaluatePermission, PermissionFor(nameof(EarlyWarningSignalsController.Evaluate)));
        Assert.Equal(EarlyWarningSignalsGuard.ManagePermission, PermissionFor(nameof(EarlyWarningSignalsController.Delete)));
        Assert.Equal(EarlyWarningSignalsGuard.AuditReadPermission, PermissionFor(nameof(EarlyWarningSignalsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_early_warning_signals_readiness", MongoEarlyWarningSignalsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_early_warning_signals_tenant_code_active", MongoEarlyWarningSignalsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_early_warning_signals_tenant_state", MongoEarlyWarningSignalsReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0043");
        var legacy = string.Join("-", "MOD", "0333");
        var runtimeStrings = new[]
        {
            EarlyWarningSignalsGuard.OwnerKey,
            EarlyWarningSignalsGuard.ReadPermission,
            EarlyWarningSignalsGuard.ManagePermission,
            EarlyWarningSignalsGuard.EvaluatePermission,
            EarlyWarningSignalsGuard.AuditReadPermission,
            MongoEarlyWarningSignalsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateEarlyWarningSignalsReadinessHandler CreateHandler(
        IEarlyWarningSignalsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static EarlyWarningSignalsReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "EarlyWarningSignals readiness",
        EarlyWarningSignalsReadinessState readinessState = EarlyWarningSignalsReadinessState.Draft,
        string sourceContractVersion = "v1",
        EarlyWarningSignalsReadinessState signalCatalogBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
        EarlyWarningSignalsReadinessState patternDetectionBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
        EarlyWarningSignalsReadinessState crossCompanyCorrelationBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
        EarlyWarningSignalsReadinessState alertRoutingBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
        EarlyWarningSignalsReadinessState signalReviewBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
        EarlyWarningSignalsReadinessState automatedDecisionBoundaryState = EarlyWarningSignalsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            EarlyWarningSignalsReadinessState = readinessState,
            SignalCatalogBoundaryState = signalCatalogBoundaryState,
            PatternDetectionBoundaryState = patternDetectionBoundaryState,
            CrossCompanyCorrelationBoundaryState = crossCompanyCorrelationBoundaryState,
            AlertRoutingBoundaryState = alertRoutingBoundaryState,
            SignalReviewBoundaryState = signalReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            RiskIndicatorSourceDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            DocumentDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            NotificationDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            ConsentPreconditionState = EarlyWarningSignalsReadinessState.Deferred,
            DataMinimizationState = EarlyWarningSignalsReadinessState.Deferred,
            RetentionPolicyState = EarlyWarningSignalsReadinessState.Deferred,
            EvidencePolicyState = EarlyWarningSignalsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, EarlyWarningSignalsReadinessState>
            {
                ["talentDataSource"] = EarlyWarningSignalsReadinessState.Ready,
                ["riskIndicatorSource"] = EarlyWarningSignalsReadinessState.Ready,
                ["signalCatalog"] = EarlyWarningSignalsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            EarlyWarningSignalsReadinessVersion = 1
        };

    private static EarlyWarningSignalsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        EarlyWarningSignalsReadinessState consentPreconditionState = EarlyWarningSignalsReadinessState.Deferred,
        EarlyWarningSignalsReadinessState dataMinimizationState = EarlyWarningSignalsReadinessState.Deferred,
        EarlyWarningSignalsReadinessState retentionPolicyState = EarlyWarningSignalsReadinessState.Deferred,
        EarlyWarningSignalsReadinessState evidencePolicyState = EarlyWarningSignalsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "EarlyWarningSignals readiness",
            EarlyWarningSignalsReadinessState = EarlyWarningSignalsReadinessState.Draft,
            SignalCatalogBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            PatternDetectionBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            CrossCompanyCorrelationBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            AlertRoutingBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            SignalReviewBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = EarlyWarningSignalsReadinessState.NotRequired,
            TalentDataSourceDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            RiskIndicatorSourceDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            DocumentDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            NotificationDependencyState = EarlyWarningSignalsReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, EarlyWarningSignalsReadinessState>
            {
                ["talentDataSource"] = EarlyWarningSignalsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            EarlyWarningSignalsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, EarlyWarningSignalsReadinessMetadata> RepositoryItems(
        InMemoryEarlyWarningSignalsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryEarlyWarningSignalsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EarlyWarningSignalsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(EarlyWarningSignalsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryEarlyWarningSignalsReadinessMetadataRepository : IEarlyWarningSignalsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, EarlyWarningSignalsReadinessMetadata> _items;

        public InMemoryEarlyWarningSignalsReadinessMetadataRepository(params EarlyWarningSignalsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EarlyWarningSignalsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EarlyWarningSignalsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<EarlyWarningSignalsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EarlyWarningSignalsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
