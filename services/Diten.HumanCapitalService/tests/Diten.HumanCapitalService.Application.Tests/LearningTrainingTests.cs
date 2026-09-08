using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.LearningTraining;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Handlers;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class LearningTrainingTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateLearningTrainingReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetLearningTrainingReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetLearningTrainingReadinessListQuery(), CancellationToken.None);
        var get = await new GetLearningTrainingReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetLearningTrainingReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("LEARNTRAIN-001", get.Data!.Code);
        Assert.Equal("Learning training readiness", get.Data.DisplayName);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.CourseCatalogBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.EnrollmentWorkflowBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.CompletionTrackingBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.CertificationBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.AssessmentScoringBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.LearningContentDependencyState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.SkillTaxonomyDependencyState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(LearningTrainingReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetLearningTrainingReadinessByIdHandler(
            new InMemoryLearningTrainingReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetLearningTrainingReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryLearningTrainingReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateLearningTrainingReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateLearningTrainingReadinessCommand(ValidRequest(code: "learntrain-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateLearningTrainingReadinessCommand(ValidRequest(code: " LEARNTRAIN-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository(metadata);
        var handler = new DeleteLearningTrainingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteLearningTrainingReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(LearningTrainingReadinessState.Archived, stored.LearningTrainingReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateLearningTrainingReadinessCommand(ValidRequest(readinessState: LearningTrainingReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(LearningTrainingReadinessState.Deferred, stored.LearningTrainingReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: LearningTrainingReadinessState.Ready,
            dataMinimizationState: LearningTrainingReadinessState.Ready,
            retentionPolicyState: LearningTrainingReadinessState.Ready,
            evidencePolicyState: LearningTrainingReadinessState.Ready);
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository(metadata);
        var handler = new EvaluateLearningTrainingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateLearningTrainingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(LearningTrainingReadinessState.Ready, response.Data!.LearningTrainingReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: LearningTrainingReadinessState.Deferred);
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository(metadata);
        var handler = new EvaluateLearningTrainingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateLearningTrainingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(LearningTrainingReadinessState.Deferred, response.Data!.LearningTrainingReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Course_enrollment_completion_certification_assessment_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryLearningTrainingReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(courseCatalogBoundaryState: LearningTrainingReadinessState.Ready),
            ValidRequest(enrollmentWorkflowBoundaryState: LearningTrainingReadinessState.Ready),
            ValidRequest(completionTrackingBoundaryState: LearningTrainingReadinessState.Ready),
            ValidRequest(certificationBoundaryState: LearningTrainingReadinessState.Ready),
            ValidRequest(assessmentScoringBoundaryState: LearningTrainingReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: LearningTrainingReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateLearningTrainingReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryLearningTrainingReadinessMetadataRepository(metadata);
        var response = await new GetLearningTrainingAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetLearningTrainingAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(LearningTrainingGuard.AuditReadPermission, PermissionFor(nameof(LearningTrainingController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryLearningTrainingReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateLearningTrainingReadinessCommand(ValidRequest(sourceContractVersion: $"v1_{marker}")),
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
            typeof(LearningTrainingReadinessCreateRequest),
            typeof(LearningTrainingReadinessDto),
            typeof(LearningTrainingReadinessMetadata)
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
        Assert.Null(typeof(LearningTrainingReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(LearningTrainingGuard.ReadPermission, PermissionFor(nameof(LearningTrainingController.GetAll)));
        Assert.Equal(LearningTrainingGuard.ReadPermission, PermissionFor(nameof(LearningTrainingController.GetById)));
        Assert.Equal(LearningTrainingGuard.ManagePermission, PermissionFor(nameof(LearningTrainingController.Create)));
        Assert.Equal(LearningTrainingGuard.EvaluatePermission, PermissionFor(nameof(LearningTrainingController.Evaluate)));
        Assert.Equal(LearningTrainingGuard.ManagePermission, PermissionFor(nameof(LearningTrainingController.Delete)));
        Assert.Equal(LearningTrainingGuard.AuditReadPermission, PermissionFor(nameof(LearningTrainingController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_learning_training_readiness", MongoLearningTrainingReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_learning_training_tenant_code_active", MongoLearningTrainingReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_learning_training_tenant_state", MongoLearningTrainingReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0030");
        var legacy = string.Join("-", "MOD", "0309");
        var runtimeStrings = new[]
        {
            LearningTrainingGuard.OwnerKey,
            LearningTrainingGuard.ReadPermission,
            LearningTrainingGuard.ManagePermission,
            LearningTrainingGuard.EvaluatePermission,
            LearningTrainingGuard.AuditReadPermission,
            MongoLearningTrainingReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateLearningTrainingReadinessHandler CreateHandler(
        ILearningTrainingReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static LearningTrainingReadinessCreateRequest ValidRequest(
        string code = "LEARNTRAIN-001",
        LearningTrainingReadinessState readinessState = LearningTrainingReadinessState.Draft,
        string sourceContractVersion = "v1",
        LearningTrainingReadinessState courseCatalogBoundaryState = LearningTrainingReadinessState.NotRequired,
        LearningTrainingReadinessState enrollmentWorkflowBoundaryState = LearningTrainingReadinessState.NotRequired,
        LearningTrainingReadinessState completionTrackingBoundaryState = LearningTrainingReadinessState.NotRequired,
        LearningTrainingReadinessState certificationBoundaryState = LearningTrainingReadinessState.NotRequired,
        LearningTrainingReadinessState assessmentScoringBoundaryState = LearningTrainingReadinessState.NotRequired,
        LearningTrainingReadinessState automatedDecisionBoundaryState = LearningTrainingReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = "Learning training readiness",
            LearningTrainingReadinessState = readinessState,
            CourseCatalogBoundaryState = courseCatalogBoundaryState,
            EnrollmentWorkflowBoundaryState = enrollmentWorkflowBoundaryState,
            CompletionTrackingBoundaryState = completionTrackingBoundaryState,
            CertificationBoundaryState = certificationBoundaryState,
            AssessmentScoringBoundaryState = assessmentScoringBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            LearningContentDependencyState = LearningTrainingReadinessState.NotRequired,
            SkillTaxonomyDependencyState = LearningTrainingReadinessState.NotRequired,
            DocumentDependencyState = LearningTrainingReadinessState.NotRequired,
            NotificationDependencyState = LearningTrainingReadinessState.NotRequired,
            ConsentPreconditionState = LearningTrainingReadinessState.Deferred,
            DataMinimizationState = LearningTrainingReadinessState.Deferred,
            RetentionPolicyState = LearningTrainingReadinessState.Deferred,
            EvidencePolicyState = LearningTrainingReadinessState.Deferred,
            DependencyStates = new Dictionary<string, LearningTrainingReadinessState>
            {
                ["employeeProjection"] = LearningTrainingReadinessState.Ready,
                ["learningContent"] = LearningTrainingReadinessState.Ready,
                ["skillFramework"] = LearningTrainingReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            LearningTrainingReadinessVersion = 1
        };

    private static LearningTrainingReadinessMetadata Metadata(
        Guid tenantId,
        string code = "LEARNTRAIN-001",
        LearningTrainingReadinessState consentPreconditionState = LearningTrainingReadinessState.Deferred,
        LearningTrainingReadinessState dataMinimizationState = LearningTrainingReadinessState.Deferred,
        LearningTrainingReadinessState retentionPolicyState = LearningTrainingReadinessState.Deferred,
        LearningTrainingReadinessState evidencePolicyState = LearningTrainingReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Learning training readiness",
            LearningTrainingReadinessState = LearningTrainingReadinessState.Draft,
            CourseCatalogBoundaryState = LearningTrainingReadinessState.NotRequired,
            EnrollmentWorkflowBoundaryState = LearningTrainingReadinessState.NotRequired,
            CompletionTrackingBoundaryState = LearningTrainingReadinessState.NotRequired,
            CertificationBoundaryState = LearningTrainingReadinessState.NotRequired,
            AssessmentScoringBoundaryState = LearningTrainingReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = LearningTrainingReadinessState.NotRequired,
            LearningContentDependencyState = LearningTrainingReadinessState.NotRequired,
            SkillTaxonomyDependencyState = LearningTrainingReadinessState.NotRequired,
            DocumentDependencyState = LearningTrainingReadinessState.NotRequired,
            NotificationDependencyState = LearningTrainingReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, LearningTrainingReadinessState>
            {
                ["learningContent"] = LearningTrainingReadinessState.Ready
            },
            SourceContractVersion = "v1",
            LearningTrainingReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, LearningTrainingReadinessMetadata> RepositoryItems(
        InMemoryLearningTrainingReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryLearningTrainingReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, LearningTrainingReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(LearningTrainingController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryLearningTrainingReadinessMetadataRepository : ILearningTrainingReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, LearningTrainingReadinessMetadata> _items;

        public InMemoryLearningTrainingReadinessMetadataRepository(params LearningTrainingReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<LearningTrainingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LearningTrainingReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<LearningTrainingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
