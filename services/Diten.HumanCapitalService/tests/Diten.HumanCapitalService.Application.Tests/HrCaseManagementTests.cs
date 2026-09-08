using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Handlers;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class HrCaseManagementTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHrCaseManagementReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHrCaseManagementReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrCaseManagementReadinessListQuery(), CancellationToken.None);
        var get = await new GetHrCaseManagementReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrCaseManagementReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HrCaseManagement readiness", get.Data.DisplayName);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.CaseIntakeBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.CaseTriageBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.InvestigationTrackingBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.DisciplinaryActionBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.ResolutionClosureBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.EmployeeRecordDependencyState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.SensitiveAccessPolicyDependencyState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HrCaseManagementReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHrCaseManagementReadinessByIdHandler(
            new InMemoryHrCaseManagementReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetHrCaseManagementReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHrCaseManagementReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHrCaseManagementReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHrCaseManagementReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHrCaseManagementReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository(metadata);
        var handler = new DeleteHrCaseManagementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteHrCaseManagementReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HrCaseManagementReadinessState.Archived, stored.HrCaseManagementReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHrCaseManagementReadinessCommand(ValidRequest(readinessState: HrCaseManagementReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrCaseManagementReadinessState.Deferred, stored.HrCaseManagementReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HrCaseManagementReadinessState.Ready,
            dataMinimizationState: HrCaseManagementReadinessState.Ready,
            retentionPolicyState: HrCaseManagementReadinessState.Ready,
            evidencePolicyState: HrCaseManagementReadinessState.Ready);
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrCaseManagementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrCaseManagementReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrCaseManagementReadinessState.Ready, response.Data!.HrCaseManagementReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HrCaseManagementReadinessState.Deferred);
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrCaseManagementReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrCaseManagementReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrCaseManagementReadinessState.Deferred, response.Data!.HrCaseManagementReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrCaseManagementReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(caseIntakeBoundaryState: HrCaseManagementReadinessState.Ready),
            ValidRequest(caseTriageBoundaryState: HrCaseManagementReadinessState.Ready),
            ValidRequest(investigationTrackingBoundaryState: HrCaseManagementReadinessState.Ready),
            ValidRequest(disciplinaryActionBoundaryState: HrCaseManagementReadinessState.Ready),
            ValidRequest(resolutionClosureBoundaryState: HrCaseManagementReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HrCaseManagementReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHrCaseManagementReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrCaseManagementReadinessMetadataRepository(metadata);
        var response = await new GetHrCaseManagementAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrCaseManagementAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HrCaseManagementGuard.AuditReadPermission, PermissionFor(nameof(HrCaseManagementController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHrCaseManagementReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHrCaseManagementReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("hr-case-management pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrCaseManagementReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHrCaseManagementReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HrCaseManagementReadinessCreateRequest),
            typeof(HrCaseManagementReadinessDto),
            typeof(HrCaseManagementReadinessMetadata)
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
        Assert.Null(typeof(HrCaseManagementReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HrCaseManagementGuard.ReadPermission, PermissionFor(nameof(HrCaseManagementController.GetAll)));
        Assert.Equal(HrCaseManagementGuard.ReadPermission, PermissionFor(nameof(HrCaseManagementController.GetById)));
        Assert.Equal(HrCaseManagementGuard.ManagePermission, PermissionFor(nameof(HrCaseManagementController.Create)));
        Assert.Equal(HrCaseManagementGuard.EvaluatePermission, PermissionFor(nameof(HrCaseManagementController.Evaluate)));
        Assert.Equal(HrCaseManagementGuard.ManagePermission, PermissionFor(nameof(HrCaseManagementController.Delete)));
        Assert.Equal(HrCaseManagementGuard.AuditReadPermission, PermissionFor(nameof(HrCaseManagementController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_hr_case_management_readiness", MongoHrCaseManagementReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_hr_case_management_tenant_code_active", MongoHrCaseManagementReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_hr_case_management_tenant_state", MongoHrCaseManagementReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0039");
        var legacy = string.Join("-", "MOD", "0317");
        var runtimeStrings = new[]
        {
            HrCaseManagementGuard.OwnerKey,
            HrCaseManagementGuard.ReadPermission,
            HrCaseManagementGuard.ManagePermission,
            HrCaseManagementGuard.EvaluatePermission,
            HrCaseManagementGuard.AuditReadPermission,
            MongoHrCaseManagementReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHrCaseManagementReadinessHandler CreateHandler(
        IHrCaseManagementReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static HrCaseManagementReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HrCaseManagement readiness",
        HrCaseManagementReadinessState readinessState = HrCaseManagementReadinessState.Draft,
        string sourceContractVersion = "v1",
        HrCaseManagementReadinessState caseIntakeBoundaryState = HrCaseManagementReadinessState.NotRequired,
        HrCaseManagementReadinessState caseTriageBoundaryState = HrCaseManagementReadinessState.NotRequired,
        HrCaseManagementReadinessState investigationTrackingBoundaryState = HrCaseManagementReadinessState.NotRequired,
        HrCaseManagementReadinessState disciplinaryActionBoundaryState = HrCaseManagementReadinessState.NotRequired,
        HrCaseManagementReadinessState resolutionClosureBoundaryState = HrCaseManagementReadinessState.NotRequired,
        HrCaseManagementReadinessState automatedDecisionBoundaryState = HrCaseManagementReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HrCaseManagementReadinessState = readinessState,
            CaseIntakeBoundaryState = caseIntakeBoundaryState,
            CaseTriageBoundaryState = caseTriageBoundaryState,
            InvestigationTrackingBoundaryState = investigationTrackingBoundaryState,
            DisciplinaryActionBoundaryState = disciplinaryActionBoundaryState,
            ResolutionClosureBoundaryState = resolutionClosureBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            EmployeeRecordDependencyState = HrCaseManagementReadinessState.NotRequired,
            SensitiveAccessPolicyDependencyState = HrCaseManagementReadinessState.NotRequired,
            DocumentDependencyState = HrCaseManagementReadinessState.NotRequired,
            NotificationDependencyState = HrCaseManagementReadinessState.NotRequired,
            ConsentPreconditionState = HrCaseManagementReadinessState.Deferred,
            DataMinimizationState = HrCaseManagementReadinessState.Deferred,
            RetentionPolicyState = HrCaseManagementReadinessState.Deferred,
            EvidencePolicyState = HrCaseManagementReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HrCaseManagementReadinessState>
            {
                ["employeeRecord"] = HrCaseManagementReadinessState.Ready,
                ["sensitiveAccessPolicy"] = HrCaseManagementReadinessState.Ready,
                ["caseIntake"] = HrCaseManagementReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HrCaseManagementReadinessVersion = 1
        };

    private static HrCaseManagementReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HrCaseManagementReadinessState consentPreconditionState = HrCaseManagementReadinessState.Deferred,
        HrCaseManagementReadinessState dataMinimizationState = HrCaseManagementReadinessState.Deferred,
        HrCaseManagementReadinessState retentionPolicyState = HrCaseManagementReadinessState.Deferred,
        HrCaseManagementReadinessState evidencePolicyState = HrCaseManagementReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "HrCaseManagement readiness",
            HrCaseManagementReadinessState = HrCaseManagementReadinessState.Draft,
            CaseIntakeBoundaryState = HrCaseManagementReadinessState.NotRequired,
            CaseTriageBoundaryState = HrCaseManagementReadinessState.NotRequired,
            InvestigationTrackingBoundaryState = HrCaseManagementReadinessState.NotRequired,
            DisciplinaryActionBoundaryState = HrCaseManagementReadinessState.NotRequired,
            ResolutionClosureBoundaryState = HrCaseManagementReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HrCaseManagementReadinessState.NotRequired,
            EmployeeRecordDependencyState = HrCaseManagementReadinessState.NotRequired,
            SensitiveAccessPolicyDependencyState = HrCaseManagementReadinessState.NotRequired,
            DocumentDependencyState = HrCaseManagementReadinessState.NotRequired,
            NotificationDependencyState = HrCaseManagementReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HrCaseManagementReadinessState>
            {
                ["employeeRecord"] = HrCaseManagementReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HrCaseManagementReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HrCaseManagementReadinessMetadata> RepositoryItems(
        InMemoryHrCaseManagementReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHrCaseManagementReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HrCaseManagementReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HrCaseManagementController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryHrCaseManagementReadinessMetadataRepository : IHrCaseManagementReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HrCaseManagementReadinessMetadata> _items;

        public InMemoryHrCaseManagementReadinessMetadataRepository(params HrCaseManagementReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HrCaseManagementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HrCaseManagementReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<HrCaseManagementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
