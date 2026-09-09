using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Handlers;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class WorkforcePlanningTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetWorkforcePlanningReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetWorkforcePlanningReadinessListQuery(), CancellationToken.None);
        var get = await new GetWorkforcePlanningReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetWorkforcePlanningReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("WorkforcePlanning readiness", get.Data.DisplayName);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.HeadcountPlanBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.DemandForecastBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.SupplyForecastBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.GapAnalysisBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.ScenarioModelingBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.OrganizationStructureDependencyState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.PositionFrameworkDependencyState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(WorkforcePlanningReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetWorkforcePlanningReadinessByIdHandler(
            new InMemoryWorkforcePlanningReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetWorkforcePlanningReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryWorkforcePlanningReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository(metadata);
        var handler = new DeleteWorkforcePlanningReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteWorkforcePlanningReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(WorkforcePlanningReadinessState.Archived, stored.WorkforcePlanningReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateWorkforcePlanningReadinessCommand(ValidRequest(readinessState: WorkforcePlanningReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(WorkforcePlanningReadinessState.Deferred, stored.WorkforcePlanningReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: WorkforcePlanningReadinessState.Ready,
            dataMinimizationState: WorkforcePlanningReadinessState.Ready,
            retentionPolicyState: WorkforcePlanningReadinessState.Ready,
            evidencePolicyState: WorkforcePlanningReadinessState.Ready);
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository(metadata);
        var handler = new EvaluateWorkforcePlanningReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateWorkforcePlanningReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(WorkforcePlanningReadinessState.Ready, response.Data!.WorkforcePlanningReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: WorkforcePlanningReadinessState.Deferred);
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository(metadata);
        var handler = new EvaluateWorkforcePlanningReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateWorkforcePlanningReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(WorkforcePlanningReadinessState.Deferred, response.Data!.WorkforcePlanningReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryWorkforcePlanningReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(headcountPlanBoundaryState: WorkforcePlanningReadinessState.Ready),
            ValidRequest(demandForecastBoundaryState: WorkforcePlanningReadinessState.Ready),
            ValidRequest(supplyForecastBoundaryState: WorkforcePlanningReadinessState.Ready),
            ValidRequest(gapAnalysisBoundaryState: WorkforcePlanningReadinessState.Ready),
            ValidRequest(scenarioModelingBoundaryState: WorkforcePlanningReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: WorkforcePlanningReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository(metadata);
        var response = await new GetWorkforcePlanningAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetWorkforcePlanningAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(WorkforcePlanningGuard.AuditReadPermission, PermissionFor(nameof(WorkforcePlanningController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryWorkforcePlanningReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateWorkforcePlanningReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("workforce-planning pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryWorkforcePlanningReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateWorkforcePlanningReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(WorkforcePlanningReadinessCreateRequest),
            typeof(WorkforcePlanningReadinessDto),
            typeof(WorkforcePlanningReadinessMetadata)
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
        Assert.Null(typeof(WorkforcePlanningReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(WorkforcePlanningGuard.ReadPermission, PermissionFor(nameof(WorkforcePlanningController.GetAll)));
        Assert.Equal(WorkforcePlanningGuard.ReadPermission, PermissionFor(nameof(WorkforcePlanningController.GetById)));
        Assert.Equal(WorkforcePlanningGuard.ManagePermission, PermissionFor(nameof(WorkforcePlanningController.Create)));
        Assert.Equal(WorkforcePlanningGuard.EvaluatePermission, PermissionFor(nameof(WorkforcePlanningController.Evaluate)));
        Assert.Equal(WorkforcePlanningGuard.ManagePermission, PermissionFor(nameof(WorkforcePlanningController.Delete)));
        Assert.Equal(WorkforcePlanningGuard.AuditReadPermission, PermissionFor(nameof(WorkforcePlanningController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_workforce_planning_readiness", MongoWorkforcePlanningReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_workforce_planning_tenant_code_active", MongoWorkforcePlanningReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_workforce_planning_tenant_state", MongoWorkforcePlanningReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0032");
        var legacy = string.Join("-", "MOD", "0310");
        var runtimeStrings = new[]
        {
            WorkforcePlanningGuard.OwnerKey,
            WorkforcePlanningGuard.ReadPermission,
            WorkforcePlanningGuard.ManagePermission,
            WorkforcePlanningGuard.EvaluatePermission,
            WorkforcePlanningGuard.AuditReadPermission,
            MongoWorkforcePlanningReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateWorkforcePlanningReadinessHandler CreateHandler(
        IWorkforcePlanningReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateWorkforcePlanningReadinessHandler CreateHandler(
        IWorkforcePlanningReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static WorkforcePlanningReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "WorkforcePlanning readiness",
        WorkforcePlanningReadinessState readinessState = WorkforcePlanningReadinessState.Draft,
        string sourceContractVersion = "v1",
        WorkforcePlanningReadinessState headcountPlanBoundaryState = WorkforcePlanningReadinessState.NotRequired,
        WorkforcePlanningReadinessState demandForecastBoundaryState = WorkforcePlanningReadinessState.NotRequired,
        WorkforcePlanningReadinessState supplyForecastBoundaryState = WorkforcePlanningReadinessState.NotRequired,
        WorkforcePlanningReadinessState gapAnalysisBoundaryState = WorkforcePlanningReadinessState.NotRequired,
        WorkforcePlanningReadinessState scenarioModelingBoundaryState = WorkforcePlanningReadinessState.NotRequired,
        WorkforcePlanningReadinessState automatedDecisionBoundaryState = WorkforcePlanningReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            WorkforcePlanningReadinessState = readinessState,
            HeadcountPlanBoundaryState = headcountPlanBoundaryState,
            DemandForecastBoundaryState = demandForecastBoundaryState,
            SupplyForecastBoundaryState = supplyForecastBoundaryState,
            GapAnalysisBoundaryState = gapAnalysisBoundaryState,
            ScenarioModelingBoundaryState = scenarioModelingBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            OrganizationStructureDependencyState = WorkforcePlanningReadinessState.NotRequired,
            PositionFrameworkDependencyState = WorkforcePlanningReadinessState.NotRequired,
            DocumentDependencyState = WorkforcePlanningReadinessState.NotRequired,
            NotificationDependencyState = WorkforcePlanningReadinessState.NotRequired,
            ConsentPreconditionState = WorkforcePlanningReadinessState.Deferred,
            DataMinimizationState = WorkforcePlanningReadinessState.Deferred,
            RetentionPolicyState = WorkforcePlanningReadinessState.Deferred,
            EvidencePolicyState = WorkforcePlanningReadinessState.Deferred,
            DependencyStates = new Dictionary<string, WorkforcePlanningReadinessState>
            {
                ["organizationStructure"] = WorkforcePlanningReadinessState.Ready,
                ["positionFramework"] = WorkforcePlanningReadinessState.Ready,
                ["headcountPlan"] = WorkforcePlanningReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            WorkforcePlanningReadinessVersion = 1
        };

    private static WorkforcePlanningReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        WorkforcePlanningReadinessState consentPreconditionState = WorkforcePlanningReadinessState.Deferred,
        WorkforcePlanningReadinessState dataMinimizationState = WorkforcePlanningReadinessState.Deferred,
        WorkforcePlanningReadinessState retentionPolicyState = WorkforcePlanningReadinessState.Deferred,
        WorkforcePlanningReadinessState evidencePolicyState = WorkforcePlanningReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "WorkforcePlanning readiness",
            WorkforcePlanningReadinessState = WorkforcePlanningReadinessState.Draft,
            HeadcountPlanBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            DemandForecastBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            SupplyForecastBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            GapAnalysisBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            ScenarioModelingBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = WorkforcePlanningReadinessState.NotRequired,
            OrganizationStructureDependencyState = WorkforcePlanningReadinessState.NotRequired,
            PositionFrameworkDependencyState = WorkforcePlanningReadinessState.NotRequired,
            DocumentDependencyState = WorkforcePlanningReadinessState.NotRequired,
            NotificationDependencyState = WorkforcePlanningReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, WorkforcePlanningReadinessState>
            {
                ["organizationStructure"] = WorkforcePlanningReadinessState.Ready
            },
            SourceContractVersion = "v1",
            WorkforcePlanningReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, WorkforcePlanningReadinessMetadata> RepositoryItems(
        InMemoryWorkforcePlanningReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryWorkforcePlanningReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, WorkforcePlanningReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(WorkforcePlanningController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<WorkforcePlanningReadinessListItemDto>> ListWith(
        IWorkforcePlanningReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetWorkforcePlanningReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetWorkforcePlanningReadinessListQuery(), CancellationToken.None);
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

    private sealed class InMemoryWorkforcePlanningReadinessMetadataRepository : IWorkforcePlanningReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, WorkforcePlanningReadinessMetadata> _items;

        public InMemoryWorkforcePlanningReadinessMetadataRepository(params WorkforcePlanningReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<WorkforcePlanningReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WorkforcePlanningReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<WorkforcePlanningReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

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
        var repository = new InMemoryWorkforcePlanningReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateWorkforcePlanningReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
