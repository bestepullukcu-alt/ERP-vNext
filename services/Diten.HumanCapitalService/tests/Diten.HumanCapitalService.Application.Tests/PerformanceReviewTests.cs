using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Handlers;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class PerformanceReviewTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetPerformanceReviewReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetPerformanceReviewReadinessListQuery(), CancellationToken.None);
        var get = await new GetPerformanceReviewReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetPerformanceReviewReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("PERFREV-001", get.Data!.Code);
        Assert.Equal("Performance review readiness", get.Data.DisplayName);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.ReviewCycleBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.GoalDependencyState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.ScoringBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.RatingBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.CalibrationBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.RankingBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.ManagerReviewUxBoundaryState);
        Assert.Equal(PerformanceReviewReadinessState.NotRequired, get.Data.EmployeeReviewUxBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetPerformanceReviewReadinessByIdHandler(
            new InMemoryPerformanceReviewReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetPerformanceReviewReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryPerformanceReviewReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "perfrev-001")), CancellationToken.None);
        var second = await handler.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: " PERFREV-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository(metadata);
        var handler = new DeletePerformanceReviewReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeletePerformanceReviewReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(PerformanceReviewReadinessState.Archived, stored.PerformanceReviewReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreatePerformanceReviewReadinessCommand(ValidRequest(readinessState: PerformanceReviewReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(PerformanceReviewReadinessState.Deferred, stored.PerformanceReviewReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: PerformanceReviewReadinessState.Ready,
            dataMinimizationState: PerformanceReviewReadinessState.Ready,
            retentionPolicyState: PerformanceReviewReadinessState.Ready,
            evidencePolicyState: PerformanceReviewReadinessState.Ready);
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository(metadata);
        var handler = new EvaluatePerformanceReviewReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluatePerformanceReviewReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PerformanceReviewReadinessState.Ready, response.Data!.PerformanceReviewReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: PerformanceReviewReadinessState.Deferred);
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository(metadata);
        var handler = new EvaluatePerformanceReviewReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluatePerformanceReviewReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PerformanceReviewReadinessState.Deferred, response.Data!.PerformanceReviewReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Workflow_scoring_rating_calibration_ranking_decision_and_ux_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryPerformanceReviewReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(reviewCycleBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(scoringBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(ratingBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(calibrationBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(rankingBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(managerReviewUxBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(employeeReviewUxBoundaryState: PerformanceReviewReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreatePerformanceReviewReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Compensation_benefits_and_payroll_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryPerformanceReviewReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(compensationDataBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(benefitsDataBoundaryState: PerformanceReviewReadinessState.Ready),
            ValidRequest(payrollDataBoundaryState: PerformanceReviewReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreatePerformanceReviewReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository(metadata);
        var response = await new GetPerformanceReviewAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetPerformanceReviewAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(PerformanceReviewGuard.AuditReadPermission, PermissionFor(nameof(PerformanceReviewsController.GetAuditMetadata)));
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
    [InlineData("salary_amount")]
    [InlineData("payroll")]
    [InlineData("benefits_election")]
    [InlineData("tax")]
    public async Task Forbidden_workflow_scoring_rating_calibration_ranking_decision_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryPerformanceReviewReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreatePerformanceReviewReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
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
            "Tax",
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
            typeof(PerformanceReviewReadinessCreateRequest),
            typeof(PerformanceReviewReadinessDto),
            typeof(PerformanceReviewReadinessMetadata)
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
        Assert.Null(typeof(PerformanceReviewReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(PerformanceReviewGuard.ReadPermission, PermissionFor(nameof(PerformanceReviewsController.GetAll)));
        Assert.Equal(PerformanceReviewGuard.ReadPermission, PermissionFor(nameof(PerformanceReviewsController.GetById)));
        Assert.Equal(PerformanceReviewGuard.ManagePermission, PermissionFor(nameof(PerformanceReviewsController.Create)));
        Assert.Equal(PerformanceReviewGuard.EvaluatePermission, PermissionFor(nameof(PerformanceReviewsController.Evaluate)));
        Assert.Equal(PerformanceReviewGuard.ManagePermission, PermissionFor(nameof(PerformanceReviewsController.Delete)));
        Assert.Equal(PerformanceReviewGuard.AuditReadPermission, PermissionFor(nameof(PerformanceReviewsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_performance_review_readiness", MongoPerformanceReviewReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_performance_review_tenant_code_active", MongoPerformanceReviewReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0027");
        var legacy = string.Join("-", "MOD", "0306");
        var runtimeStrings = new[]
        {
            PerformanceReviewGuard.OwnerKey,
            PerformanceReviewGuard.ReadPermission,
            PerformanceReviewGuard.ManagePermission,
            PerformanceReviewGuard.EvaluatePermission,
            PerformanceReviewGuard.AuditReadPermission,
            MongoPerformanceReviewReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreatePerformanceReviewReadinessHandler CreateHandler(
        IPerformanceReviewReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreatePerformanceReviewReadinessHandler CreateHandler(
        IPerformanceReviewReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static PerformanceReviewReadinessCreateRequest ValidRequest(
        string code = "PERFREV-001",
        PerformanceReviewReadinessState readinessState = PerformanceReviewReadinessState.Draft,
        string sourceContractVersion = "v1",
        PerformanceReviewReadinessState reviewCycleBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState goalDependencyState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState scoringBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState ratingBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState calibrationBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState rankingBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState automatedDecisionBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState managerReviewUxBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState employeeReviewUxBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState compensationDataBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState benefitsDataBoundaryState = PerformanceReviewReadinessState.NotRequired,
        PerformanceReviewReadinessState payrollDataBoundaryState = PerformanceReviewReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = "Performance review readiness",
            PerformanceReviewReadinessState = readinessState,
            ReviewCycleBoundaryState = reviewCycleBoundaryState,
            GoalDependencyState = goalDependencyState,
            ScoringBoundaryState = scoringBoundaryState,
            RatingBoundaryState = ratingBoundaryState,
            CalibrationBoundaryState = calibrationBoundaryState,
            RankingBoundaryState = rankingBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            ManagerReviewUxBoundaryState = managerReviewUxBoundaryState,
            EmployeeReviewUxBoundaryState = employeeReviewUxBoundaryState,
            CompensationDataBoundaryState = compensationDataBoundaryState,
            BenefitsDataBoundaryState = benefitsDataBoundaryState,
            PayrollDataBoundaryState = payrollDataBoundaryState,
            DocumentDependencyState = PerformanceReviewReadinessState.NotRequired,
            NotificationDependencyState = PerformanceReviewReadinessState.NotRequired,
            ConsentPreconditionState = PerformanceReviewReadinessState.Deferred,
            DataMinimizationState = PerformanceReviewReadinessState.Deferred,
            RetentionPolicyState = PerformanceReviewReadinessState.Deferred,
            EvidencePolicyState = PerformanceReviewReadinessState.Deferred,
            DependencyStates = new Dictionary<string, PerformanceReviewReadinessState>
            {
                ["employeeProjection"] = PerformanceReviewReadinessState.Ready,
                ["reviewTemplateBoundary"] = PerformanceReviewReadinessState.Ready,
                ["employeeOnboarding"] = PerformanceReviewReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            PerformanceReviewReadinessVersion = 1
        };

    private static PerformanceReviewReadinessMetadata Metadata(
        Guid tenantId,
        string code = "PERFREV-001",
        PerformanceReviewReadinessState consentPreconditionState = PerformanceReviewReadinessState.Deferred,
        PerformanceReviewReadinessState dataMinimizationState = PerformanceReviewReadinessState.Deferred,
        PerformanceReviewReadinessState retentionPolicyState = PerformanceReviewReadinessState.Deferred,
        PerformanceReviewReadinessState evidencePolicyState = PerformanceReviewReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "Performance review readiness",
            PerformanceReviewReadinessState = PerformanceReviewReadinessState.Draft,
            ReviewCycleBoundaryState = PerformanceReviewReadinessState.NotRequired,
            GoalDependencyState = PerformanceReviewReadinessState.NotRequired,
            ScoringBoundaryState = PerformanceReviewReadinessState.NotRequired,
            RatingBoundaryState = PerformanceReviewReadinessState.NotRequired,
            CalibrationBoundaryState = PerformanceReviewReadinessState.NotRequired,
            RankingBoundaryState = PerformanceReviewReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = PerformanceReviewReadinessState.NotRequired,
            ManagerReviewUxBoundaryState = PerformanceReviewReadinessState.NotRequired,
            EmployeeReviewUxBoundaryState = PerformanceReviewReadinessState.NotRequired,
            CompensationDataBoundaryState = PerformanceReviewReadinessState.NotRequired,
            BenefitsDataBoundaryState = PerformanceReviewReadinessState.NotRequired,
            PayrollDataBoundaryState = PerformanceReviewReadinessState.NotRequired,
            DocumentDependencyState = PerformanceReviewReadinessState.NotRequired,
            NotificationDependencyState = PerformanceReviewReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, PerformanceReviewReadinessState>
            {
                ["employeeOnboarding"] = PerformanceReviewReadinessState.Ready
            },
            SourceContractVersion = "v1",
            PerformanceReviewReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, PerformanceReviewReadinessMetadata> RepositoryItems(
        InMemoryPerformanceReviewReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryPerformanceReviewReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, PerformanceReviewReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(PerformanceReviewsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<PerformanceReviewReadinessListItemDto>> ListWith(
        IPerformanceReviewReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetPerformanceReviewReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetPerformanceReviewReadinessListQuery(), CancellationToken.None);
        return response.Data!;
    }

    private sealed class FixedLegalEntityContext : ILegalEntityContext
    {
        private readonly IReadOnlyCollection<Guid> _effective;
        private readonly bool _selectionAllowed;

        public FixedLegalEntityContext(
            Guid? selected,
            IReadOnlyCollection<Guid>? effective = null,
            bool? selectionAllowed = null)
        {
            SelectedLegalEntityId = selected;
            _effective = effective ?? (selected is { } s ? new[] { s } : Array.Empty<Guid>());
            _selectionAllowed = selectionAllowed ?? selected.HasValue;
        }

        public Guid? SelectedLegalEntityId { get; }

        public Task<bool> IsSelectionAllowedAsync(CancellationToken ct) => Task.FromResult(_selectionAllowed);

        public Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct) =>
            Task.FromResult(_effective);
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryPerformanceReviewReadinessMetadataRepository : IPerformanceReviewReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, PerformanceReviewReadinessMetadata> _items;

        public InMemoryPerformanceReviewReadinessMetadataRepository(params PerformanceReviewReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<PerformanceReviewReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PerformanceReviewReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<PerformanceReviewReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

        var medikalOnly = await ListWith(repository, tenantId, new[] { Medikal });
        var teknolojiOnly = await ListWith(repository, tenantId, new[] { Teknoloji });
        var holdingRollup = await ListWith(repository, tenantId, new[] { Holding, Medikal, Teknoloji });

        Assert.Equal(new[] { "MED-01" }, medikalOnly.Select(x => x.Code).ToArray());
        Assert.Equal(new[] { "TEK-01" }, teknolojiOnly.Select(x => x.Code).ToArray());
        Assert.Equal(new[] { "MED-01", "TEK-01" }, holdingRollup.Select(x => x.Code).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task LegalEntity_same_code_is_unique_per_legal_entity_not_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPerformanceReviewReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreatePerformanceReviewReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
