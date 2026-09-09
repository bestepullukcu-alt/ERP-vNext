using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Handlers;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class DevelopmentPlanTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetDevelopmentPlanReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetDevelopmentPlanReadinessListQuery(), CancellationToken.None);
        var get = await new GetDevelopmentPlanReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetDevelopmentPlanReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("DEVPLAN-001", get.Data!.Code);
        Assert.Equal("Development plan readiness", get.Data.DisplayName);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.DevelopmentPlanWorkflowBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.GoalAssignmentBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.LearningAssignmentBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.SkillGapScoringBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.RatingBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.RecommendationBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.RankingBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.ManagerActionUxBoundaryState);
        Assert.Equal(DevelopmentPlanReadinessState.NotRequired, get.Data.CoachingActionBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetDevelopmentPlanReadinessByIdHandler(
            new InMemoryDevelopmentPlanReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetDevelopmentPlanReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryDevelopmentPlanReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "devplan-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: " DEVPLAN-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository(metadata);
        var handler = new DeleteDevelopmentPlanReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteDevelopmentPlanReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(DevelopmentPlanReadinessState.Archived, stored.DevelopmentPlanReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateDevelopmentPlanReadinessCommand(ValidRequest(readinessState: DevelopmentPlanReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(DevelopmentPlanReadinessState.Deferred, stored.DevelopmentPlanReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: DevelopmentPlanReadinessState.Ready,
            dataMinimizationState: DevelopmentPlanReadinessState.Ready,
            retentionPolicyState: DevelopmentPlanReadinessState.Ready,
            evidencePolicyState: DevelopmentPlanReadinessState.Ready);
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository(metadata);
        var handler = new EvaluateDevelopmentPlanReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateDevelopmentPlanReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(DevelopmentPlanReadinessState.Ready, response.Data!.DevelopmentPlanReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: DevelopmentPlanReadinessState.Deferred);
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository(metadata);
        var handler = new EvaluateDevelopmentPlanReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateDevelopmentPlanReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(DevelopmentPlanReadinessState.Deferred, response.Data!.DevelopmentPlanReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Workflow_assignment_coaching_skillGapScoring_rating_recommendation_ranking_decision_and_ux_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryDevelopmentPlanReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(developmentPlanWorkflowBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(goalAssignmentBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(learningAssignmentBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(skillGapScoringBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(ratingBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(recommendationBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(rankingBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(managerActionUxBoundaryState: DevelopmentPlanReadinessState.Ready),
            ValidRequest(coachingActionBoundaryState: DevelopmentPlanReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository(metadata);
        var response = await new GetDevelopmentPlanAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetDevelopmentPlanAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(DevelopmentPlanGuard.AuditReadPermission, PermissionFor(nameof(DevelopmentPlansController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("workflow_body")]
    [InlineData("plan_body")]
    [InlineData("development_notes")]
    [InlineData("goal_assignment_payload")]
    [InlineData("learning_assignment_payload")]
    [InlineData("coaching_payload")]
    [InlineData("coaching_narrative")]
    [InlineData("review_note")]
    [InlineData("appraisal_narrative")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    [InlineData("score")]
    [InlineData("rating")]
    [InlineData("recommendation")]
    [InlineData("rank")]
    [InlineData("model_output")]
    [InlineData("automated_decision")]
    [InlineData("salary_amount")]
    [InlineData("payroll")]
    [InlineData("benefits_election")]
    [InlineData("tax")]
    public async Task Forbidden_workflow_assignment_coaching_skillGapScoring_rating_recommendation_ranking_decision_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryDevelopmentPlanReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateDevelopmentPlanReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
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
            "PlanBody",
            "ManagerNote",
            "HrNote",
            "EmployeeStatement",
            "DevelopmentNote",
            "CoachingNarrative",
            "ReviewNote",
            "Appraisal",
            "FreeText",
            "Narrative",
            "GoalScore",
            "RatingValue",
            "RankValue",
            "RecommendationOutput",
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
            typeof(DevelopmentPlanReadinessCreateRequest),
            typeof(DevelopmentPlanReadinessDto),
            typeof(DevelopmentPlanReadinessMetadata)
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
        Assert.Null(typeof(DevelopmentPlanReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(DevelopmentPlanGuard.ReadPermission, PermissionFor(nameof(DevelopmentPlansController.GetAll)));
        Assert.Equal(DevelopmentPlanGuard.ReadPermission, PermissionFor(nameof(DevelopmentPlansController.GetById)));
        Assert.Equal(DevelopmentPlanGuard.ManagePermission, PermissionFor(nameof(DevelopmentPlansController.Create)));
        Assert.Equal(DevelopmentPlanGuard.EvaluatePermission, PermissionFor(nameof(DevelopmentPlansController.Evaluate)));
        Assert.Equal(DevelopmentPlanGuard.ManagePermission, PermissionFor(nameof(DevelopmentPlansController.Delete)));
        Assert.Equal(DevelopmentPlanGuard.AuditReadPermission, PermissionFor(nameof(DevelopmentPlansController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_development_plans_readiness", MongoDevelopmentPlanReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_development_plans_tenant_code_active", MongoDevelopmentPlanReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0029");
        var legacy = string.Join("-", "MOD", "0308");
        var runtimeStrings = new[]
        {
            DevelopmentPlanGuard.OwnerKey,
            DevelopmentPlanGuard.ReadPermission,
            DevelopmentPlanGuard.ManagePermission,
            DevelopmentPlanGuard.EvaluatePermission,
            DevelopmentPlanGuard.AuditReadPermission,
            MongoDevelopmentPlanReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateDevelopmentPlanReadinessHandler CreateHandler(
        IDevelopmentPlanReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateDevelopmentPlanReadinessHandler CreateHandler(
        IDevelopmentPlanReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static DevelopmentPlanReadinessCreateRequest ValidRequest(
        string code = "DEVPLAN-001",
        DevelopmentPlanReadinessState readinessState = DevelopmentPlanReadinessState.Draft,
        string sourceContractVersion = "v1",
        DevelopmentPlanReadinessState developmentPlanWorkflowBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState goalAssignmentBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState learningAssignmentBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState skillGapScoringBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState ratingBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState recommendationBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState rankingBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState automatedDecisionBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState managerActionUxBoundaryState = DevelopmentPlanReadinessState.NotRequired,
        DevelopmentPlanReadinessState coachingActionBoundaryState = DevelopmentPlanReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = "Development plan readiness",
            DevelopmentPlanReadinessState = readinessState,
            DevelopmentPlanWorkflowBoundaryState = developmentPlanWorkflowBoundaryState,
            GoalAssignmentBoundaryState = goalAssignmentBoundaryState,
            LearningAssignmentBoundaryState = learningAssignmentBoundaryState,
            SkillGapScoringBoundaryState = skillGapScoringBoundaryState,
            RatingBoundaryState = ratingBoundaryState,
            RecommendationBoundaryState = recommendationBoundaryState,
            RankingBoundaryState = rankingBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            ManagerActionUxBoundaryState = managerActionUxBoundaryState,
            CoachingActionBoundaryState = coachingActionBoundaryState,
            DocumentDependencyState = DevelopmentPlanReadinessState.NotRequired,
            NotificationDependencyState = DevelopmentPlanReadinessState.NotRequired,
            ConsentPreconditionState = DevelopmentPlanReadinessState.Deferred,
            DataMinimizationState = DevelopmentPlanReadinessState.Deferred,
            RetentionPolicyState = DevelopmentPlanReadinessState.Deferred,
            EvidencePolicyState = DevelopmentPlanReadinessState.Deferred,
            DependencyStates = new Dictionary<string, DevelopmentPlanReadinessState>
            {
                ["employeeProjection"] = DevelopmentPlanReadinessState.Ready,
                ["competencyFramework"] = DevelopmentPlanReadinessState.Ready,
                ["skillsCatalog"] = DevelopmentPlanReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            DevelopmentPlanReadinessVersion = 1
        };

    private static DevelopmentPlanReadinessMetadata Metadata(
        Guid tenantId,
        string code = "DEVPLAN-001",
        DevelopmentPlanReadinessState consentPreconditionState = DevelopmentPlanReadinessState.Deferred,
        DevelopmentPlanReadinessState dataMinimizationState = DevelopmentPlanReadinessState.Deferred,
        DevelopmentPlanReadinessState retentionPolicyState = DevelopmentPlanReadinessState.Deferred,
        DevelopmentPlanReadinessState evidencePolicyState = DevelopmentPlanReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "Development plan readiness",
            DevelopmentPlanReadinessState = DevelopmentPlanReadinessState.Draft,
            DevelopmentPlanWorkflowBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            GoalAssignmentBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            LearningAssignmentBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            SkillGapScoringBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            RatingBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            RecommendationBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            RankingBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            ManagerActionUxBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            CoachingActionBoundaryState = DevelopmentPlanReadinessState.NotRequired,
            DocumentDependencyState = DevelopmentPlanReadinessState.NotRequired,
            NotificationDependencyState = DevelopmentPlanReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, DevelopmentPlanReadinessState>
            {
                ["competencyFramework"] = DevelopmentPlanReadinessState.Ready
            },
            SourceContractVersion = "v1",
            DevelopmentPlanReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, DevelopmentPlanReadinessMetadata> RepositoryItems(
        InMemoryDevelopmentPlanReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryDevelopmentPlanReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, DevelopmentPlanReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(DevelopmentPlansController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<DevelopmentPlanReadinessListItemDto>> ListWith(
        IDevelopmentPlanReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetDevelopmentPlanReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetDevelopmentPlanReadinessListQuery(), CancellationToken.None);
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

    private sealed class InMemoryDevelopmentPlanReadinessMetadataRepository : IDevelopmentPlanReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, DevelopmentPlanReadinessMetadata> _items;

        public InMemoryDevelopmentPlanReadinessMetadataRepository(params DevelopmentPlanReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<DevelopmentPlanReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DevelopmentPlanReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<DevelopmentPlanReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

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
        var repository = new InMemoryDevelopmentPlanReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateDevelopmentPlanReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
