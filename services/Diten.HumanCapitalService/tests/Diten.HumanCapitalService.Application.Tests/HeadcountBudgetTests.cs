using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Handlers;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class HeadcountBudgetTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHeadcountBudgetReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHeadcountBudgetReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHeadcountBudgetReadinessListQuery(), CancellationToken.None);
        var get = await new GetHeadcountBudgetReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHeadcountBudgetReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HeadcountBudget readiness", get.Data.DisplayName);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.HeadcountRequisitionBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.PositionBudgetBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.BudgetAllocationBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.BudgetApprovalBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.BudgetReconciliationBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.OrganizationStructureDependencyState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.PositionFrameworkDependencyState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HeadcountBudgetReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHeadcountBudgetReadinessByIdHandler(
            new InMemoryHeadcountBudgetReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetHeadcountBudgetReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHeadcountBudgetReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHeadcountBudgetReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHeadcountBudgetReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHeadcountBudgetReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository(metadata);
        var handler = new DeleteHeadcountBudgetReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteHeadcountBudgetReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HeadcountBudgetReadinessState.Archived, stored.HeadcountBudgetReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHeadcountBudgetReadinessCommand(ValidRequest(readinessState: HeadcountBudgetReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HeadcountBudgetReadinessState.Deferred, stored.HeadcountBudgetReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HeadcountBudgetReadinessState.Ready,
            dataMinimizationState: HeadcountBudgetReadinessState.Ready,
            retentionPolicyState: HeadcountBudgetReadinessState.Ready,
            evidencePolicyState: HeadcountBudgetReadinessState.Ready);
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository(metadata);
        var handler = new EvaluateHeadcountBudgetReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHeadcountBudgetReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HeadcountBudgetReadinessState.Ready, response.Data!.HeadcountBudgetReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HeadcountBudgetReadinessState.Deferred);
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository(metadata);
        var handler = new EvaluateHeadcountBudgetReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHeadcountBudgetReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HeadcountBudgetReadinessState.Deferred, response.Data!.HeadcountBudgetReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHeadcountBudgetReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(headcountRequisitionBoundaryState: HeadcountBudgetReadinessState.Ready),
            ValidRequest(positionBudgetBoundaryState: HeadcountBudgetReadinessState.Ready),
            ValidRequest(budgetAllocationBoundaryState: HeadcountBudgetReadinessState.Ready),
            ValidRequest(budgetApprovalBoundaryState: HeadcountBudgetReadinessState.Ready),
            ValidRequest(budgetReconciliationBoundaryState: HeadcountBudgetReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HeadcountBudgetReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHeadcountBudgetReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHeadcountBudgetReadinessMetadataRepository(metadata);
        var response = await new GetHeadcountBudgetAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHeadcountBudgetAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HeadcountBudgetGuard.AuditReadPermission, PermissionFor(nameof(HeadcountBudgetController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHeadcountBudgetReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHeadcountBudgetReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("headcount-budget pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHeadcountBudgetReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHeadcountBudgetReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HeadcountBudgetReadinessCreateRequest),
            typeof(HeadcountBudgetReadinessDto),
            typeof(HeadcountBudgetReadinessMetadata)
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
        Assert.Null(typeof(HeadcountBudgetReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HeadcountBudgetGuard.ReadPermission, PermissionFor(nameof(HeadcountBudgetController.GetAll)));
        Assert.Equal(HeadcountBudgetGuard.ReadPermission, PermissionFor(nameof(HeadcountBudgetController.GetById)));
        Assert.Equal(HeadcountBudgetGuard.ManagePermission, PermissionFor(nameof(HeadcountBudgetController.Create)));
        Assert.Equal(HeadcountBudgetGuard.EvaluatePermission, PermissionFor(nameof(HeadcountBudgetController.Evaluate)));
        Assert.Equal(HeadcountBudgetGuard.ManagePermission, PermissionFor(nameof(HeadcountBudgetController.Delete)));
        Assert.Equal(HeadcountBudgetGuard.AuditReadPermission, PermissionFor(nameof(HeadcountBudgetController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_headcount_budget_readiness", MongoHeadcountBudgetReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_headcount_budget_tenant_code_active", MongoHeadcountBudgetReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_headcount_budget_tenant_state", MongoHeadcountBudgetReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0033");
        var legacy = string.Join("-", "MOD", "0311");
        var runtimeStrings = new[]
        {
            HeadcountBudgetGuard.OwnerKey,
            HeadcountBudgetGuard.ReadPermission,
            HeadcountBudgetGuard.ManagePermission,
            HeadcountBudgetGuard.EvaluatePermission,
            HeadcountBudgetGuard.AuditReadPermission,
            MongoHeadcountBudgetReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHeadcountBudgetReadinessHandler CreateHandler(
        IHeadcountBudgetReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static HeadcountBudgetReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HeadcountBudget readiness",
        HeadcountBudgetReadinessState readinessState = HeadcountBudgetReadinessState.Draft,
        string sourceContractVersion = "v1",
        HeadcountBudgetReadinessState headcountRequisitionBoundaryState = HeadcountBudgetReadinessState.NotRequired,
        HeadcountBudgetReadinessState positionBudgetBoundaryState = HeadcountBudgetReadinessState.NotRequired,
        HeadcountBudgetReadinessState budgetAllocationBoundaryState = HeadcountBudgetReadinessState.NotRequired,
        HeadcountBudgetReadinessState budgetApprovalBoundaryState = HeadcountBudgetReadinessState.NotRequired,
        HeadcountBudgetReadinessState budgetReconciliationBoundaryState = HeadcountBudgetReadinessState.NotRequired,
        HeadcountBudgetReadinessState automatedDecisionBoundaryState = HeadcountBudgetReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HeadcountBudgetReadinessState = readinessState,
            HeadcountRequisitionBoundaryState = headcountRequisitionBoundaryState,
            PositionBudgetBoundaryState = positionBudgetBoundaryState,
            BudgetAllocationBoundaryState = budgetAllocationBoundaryState,
            BudgetApprovalBoundaryState = budgetApprovalBoundaryState,
            BudgetReconciliationBoundaryState = budgetReconciliationBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            OrganizationStructureDependencyState = HeadcountBudgetReadinessState.NotRequired,
            PositionFrameworkDependencyState = HeadcountBudgetReadinessState.NotRequired,
            DocumentDependencyState = HeadcountBudgetReadinessState.NotRequired,
            NotificationDependencyState = HeadcountBudgetReadinessState.NotRequired,
            ConsentPreconditionState = HeadcountBudgetReadinessState.Deferred,
            DataMinimizationState = HeadcountBudgetReadinessState.Deferred,
            RetentionPolicyState = HeadcountBudgetReadinessState.Deferred,
            EvidencePolicyState = HeadcountBudgetReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HeadcountBudgetReadinessState>
            {
                ["organizationStructure"] = HeadcountBudgetReadinessState.Ready,
                ["positionFramework"] = HeadcountBudgetReadinessState.Ready,
                ["headcountRequisition"] = HeadcountBudgetReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HeadcountBudgetReadinessVersion = 1
        };

    private static HeadcountBudgetReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HeadcountBudgetReadinessState consentPreconditionState = HeadcountBudgetReadinessState.Deferred,
        HeadcountBudgetReadinessState dataMinimizationState = HeadcountBudgetReadinessState.Deferred,
        HeadcountBudgetReadinessState retentionPolicyState = HeadcountBudgetReadinessState.Deferred,
        HeadcountBudgetReadinessState evidencePolicyState = HeadcountBudgetReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "HeadcountBudget readiness",
            HeadcountBudgetReadinessState = HeadcountBudgetReadinessState.Draft,
            HeadcountRequisitionBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            PositionBudgetBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            BudgetAllocationBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            BudgetApprovalBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            BudgetReconciliationBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HeadcountBudgetReadinessState.NotRequired,
            OrganizationStructureDependencyState = HeadcountBudgetReadinessState.NotRequired,
            PositionFrameworkDependencyState = HeadcountBudgetReadinessState.NotRequired,
            DocumentDependencyState = HeadcountBudgetReadinessState.NotRequired,
            NotificationDependencyState = HeadcountBudgetReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HeadcountBudgetReadinessState>
            {
                ["organizationStructure"] = HeadcountBudgetReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HeadcountBudgetReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HeadcountBudgetReadinessMetadata> RepositoryItems(
        InMemoryHeadcountBudgetReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHeadcountBudgetReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HeadcountBudgetReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HeadcountBudgetController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryHeadcountBudgetReadinessMetadataRepository : IHeadcountBudgetReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HeadcountBudgetReadinessMetadata> _items;

        public InMemoryHeadcountBudgetReadinessMetadataRepository(params HeadcountBudgetReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HeadcountBudgetReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HeadcountBudgetReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<HeadcountBudgetReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
