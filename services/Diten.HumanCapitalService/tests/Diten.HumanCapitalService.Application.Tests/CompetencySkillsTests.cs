using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompetencySkills;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Handlers;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class CompetencySkillsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateCompetencySkillsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetCompetencySkillsReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCompetencySkillsReadinessListQuery(), CancellationToken.None);
        var get = await new GetCompetencySkillsReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCompetencySkillsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("COMPSKILL-001", get.Data!.Code);
        Assert.Equal("Competency skills readiness", get.Data.DisplayName);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.AssessmentWorkflowBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.CompetencyFrameworkDependencyState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.SkillTaxonomyDependencyState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.SkillScoringBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.RatingBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.CalibrationBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.RankingBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.ManagerAssessmentUxBoundaryState);
        Assert.Equal(CompetencySkillsReadinessState.NotRequired, get.Data.EmployeeAssessmentUxBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetCompetencySkillsReadinessByIdHandler(
            new InMemoryCompetencySkillsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetCompetencySkillsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryCompetencySkillsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateCompetencySkillsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateCompetencySkillsReadinessCommand(ValidRequest(code: "compskill-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateCompetencySkillsReadinessCommand(ValidRequest(code: " COMPSKILL-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository(metadata);
        var handler = new DeleteCompetencySkillsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteCompetencySkillsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(CompetencySkillsReadinessState.Archived, stored.CompetencySkillsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateCompetencySkillsReadinessCommand(ValidRequest(readinessState: CompetencySkillsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompetencySkillsReadinessState.Deferred, stored.CompetencySkillsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: CompetencySkillsReadinessState.Ready,
            dataMinimizationState: CompetencySkillsReadinessState.Ready,
            retentionPolicyState: CompetencySkillsReadinessState.Ready,
            evidencePolicyState: CompetencySkillsReadinessState.Ready);
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository(metadata);
        var handler = new EvaluateCompetencySkillsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateCompetencySkillsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompetencySkillsReadinessState.Ready, response.Data!.CompetencySkillsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: CompetencySkillsReadinessState.Deferred);
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository(metadata);
        var handler = new EvaluateCompetencySkillsReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateCompetencySkillsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompetencySkillsReadinessState.Deferred, response.Data!.CompetencySkillsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Workflow_skillScoring_rating_calibration_ranking_decision_and_ux_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryCompetencySkillsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(assessmentWorkflowBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(skillScoringBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(ratingBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(calibrationBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(rankingBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(managerAssessmentUxBoundaryState: CompetencySkillsReadinessState.Ready),
            ValidRequest(employeeAssessmentUxBoundaryState: CompetencySkillsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateCompetencySkillsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCompetencySkillsReadinessMetadataRepository(metadata);
        var response = await new GetCompetencySkillsAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCompetencySkillsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(CompetencySkillsGuard.AuditReadPermission, PermissionFor(nameof(CompetencySkillsController.GetAuditMetadata)));
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
    public async Task Forbidden_workflow_skillScoring_rating_calibration_ranking_decision_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryCompetencySkillsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateCompetencySkillsReadinessCommand(ValidRequest(sourceContractVersion: $"v1_{marker}")),
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
            typeof(CompetencySkillsReadinessCreateRequest),
            typeof(CompetencySkillsReadinessDto),
            typeof(CompetencySkillsReadinessMetadata)
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
        Assert.Null(typeof(CompetencySkillsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(CompetencySkillsGuard.ReadPermission, PermissionFor(nameof(CompetencySkillsController.GetAll)));
        Assert.Equal(CompetencySkillsGuard.ReadPermission, PermissionFor(nameof(CompetencySkillsController.GetById)));
        Assert.Equal(CompetencySkillsGuard.ManagePermission, PermissionFor(nameof(CompetencySkillsController.Create)));
        Assert.Equal(CompetencySkillsGuard.EvaluatePermission, PermissionFor(nameof(CompetencySkillsController.Evaluate)));
        Assert.Equal(CompetencySkillsGuard.ManagePermission, PermissionFor(nameof(CompetencySkillsController.Delete)));
        Assert.Equal(CompetencySkillsGuard.AuditReadPermission, PermissionFor(nameof(CompetencySkillsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_competency_skills_readiness", MongoCompetencySkillsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_competency_skills_tenant_code_active", MongoCompetencySkillsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0028");
        var legacy = string.Join("-", "MOD", "0307");
        var runtimeStrings = new[]
        {
            CompetencySkillsGuard.OwnerKey,
            CompetencySkillsGuard.ReadPermission,
            CompetencySkillsGuard.ManagePermission,
            CompetencySkillsGuard.EvaluatePermission,
            CompetencySkillsGuard.AuditReadPermission,
            MongoCompetencySkillsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateCompetencySkillsReadinessHandler CreateHandler(
        ICompetencySkillsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static CompetencySkillsReadinessCreateRequest ValidRequest(
        string code = "COMPSKILL-001",
        CompetencySkillsReadinessState readinessState = CompetencySkillsReadinessState.Draft,
        string sourceContractVersion = "v1",
        CompetencySkillsReadinessState assessmentWorkflowBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState competencyFrameworkDependencyState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState skillTaxonomyDependencyState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState skillScoringBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState ratingBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState calibrationBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState rankingBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState automatedDecisionBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState managerAssessmentUxBoundaryState = CompetencySkillsReadinessState.NotRequired,
        CompetencySkillsReadinessState employeeAssessmentUxBoundaryState = CompetencySkillsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = "Competency skills readiness",
            CompetencySkillsReadinessState = readinessState,
            AssessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState,
            CompetencyFrameworkDependencyState = competencyFrameworkDependencyState,
            SkillTaxonomyDependencyState = skillTaxonomyDependencyState,
            SkillScoringBoundaryState = skillScoringBoundaryState,
            RatingBoundaryState = ratingBoundaryState,
            CalibrationBoundaryState = calibrationBoundaryState,
            RankingBoundaryState = rankingBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            ManagerAssessmentUxBoundaryState = managerAssessmentUxBoundaryState,
            EmployeeAssessmentUxBoundaryState = employeeAssessmentUxBoundaryState,
            DocumentDependencyState = CompetencySkillsReadinessState.NotRequired,
            NotificationDependencyState = CompetencySkillsReadinessState.NotRequired,
            ConsentPreconditionState = CompetencySkillsReadinessState.Deferred,
            DataMinimizationState = CompetencySkillsReadinessState.Deferred,
            RetentionPolicyState = CompetencySkillsReadinessState.Deferred,
            EvidencePolicyState = CompetencySkillsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, CompetencySkillsReadinessState>
            {
                ["employeeProjection"] = CompetencySkillsReadinessState.Ready,
                ["competencyFramework"] = CompetencySkillsReadinessState.Ready,
                ["skillsCatalog"] = CompetencySkillsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            CompetencySkillsReadinessVersion = 1
        };

    private static CompetencySkillsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "COMPSKILL-001",
        CompetencySkillsReadinessState consentPreconditionState = CompetencySkillsReadinessState.Deferred,
        CompetencySkillsReadinessState dataMinimizationState = CompetencySkillsReadinessState.Deferred,
        CompetencySkillsReadinessState retentionPolicyState = CompetencySkillsReadinessState.Deferred,
        CompetencySkillsReadinessState evidencePolicyState = CompetencySkillsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Competency skills readiness",
            CompetencySkillsReadinessState = CompetencySkillsReadinessState.Draft,
            AssessmentWorkflowBoundaryState = CompetencySkillsReadinessState.NotRequired,
            CompetencyFrameworkDependencyState = CompetencySkillsReadinessState.NotRequired,
            SkillTaxonomyDependencyState = CompetencySkillsReadinessState.NotRequired,
            SkillScoringBoundaryState = CompetencySkillsReadinessState.NotRequired,
            RatingBoundaryState = CompetencySkillsReadinessState.NotRequired,
            CalibrationBoundaryState = CompetencySkillsReadinessState.NotRequired,
            RankingBoundaryState = CompetencySkillsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = CompetencySkillsReadinessState.NotRequired,
            ManagerAssessmentUxBoundaryState = CompetencySkillsReadinessState.NotRequired,
            EmployeeAssessmentUxBoundaryState = CompetencySkillsReadinessState.NotRequired,
            DocumentDependencyState = CompetencySkillsReadinessState.NotRequired,
            NotificationDependencyState = CompetencySkillsReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, CompetencySkillsReadinessState>
            {
                ["competencyFramework"] = CompetencySkillsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            CompetencySkillsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, CompetencySkillsReadinessMetadata> RepositoryItems(
        InMemoryCompetencySkillsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryCompetencySkillsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, CompetencySkillsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(CompetencySkillsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryCompetencySkillsReadinessMetadataRepository : ICompetencySkillsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, CompetencySkillsReadinessMetadata> _items;

        public InMemoryCompetencySkillsReadinessMetadataRepository(params CompetencySkillsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<CompetencySkillsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CompetencySkillsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<CompetencySkillsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
