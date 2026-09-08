using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Handlers;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class EmploymentChangeTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateEmploymentChangeReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetEmploymentChangeReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmploymentChangeReadinessListQuery(), CancellationToken.None);
        var get = await new GetEmploymentChangeReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmploymentChangeReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("EMPCHANGE-001", get.Data!.Code);
        Assert.Equal("Employment change readiness", get.Data.DisplayName);
        Assert.Equal(EmploymentChangeReadinessState.NotRequired, get.Data.ChangeLifecycleBoundaryState);
        Assert.Equal(EmploymentChangeReadinessState.NotRequired, get.Data.TransferBoundaryState);
        Assert.Equal(EmploymentChangeReadinessState.NotRequired, get.Data.PromotionBoundaryState);
        Assert.Equal(EmploymentChangeReadinessState.NotRequired, get.Data.ApprovalBoundaryState);
        Assert.Equal(EmploymentChangeReadinessState.NotRequired, get.Data.PositionAssignmentBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetEmploymentChangeReadinessByIdHandler(
            new InMemoryEmploymentChangeReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetEmploymentChangeReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryEmploymentChangeReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateEmploymentChangeReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateEmploymentChangeReadinessCommand(ValidRequest(code: "empchange-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateEmploymentChangeReadinessCommand(ValidRequest(code: " EMPCHANGE-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository(metadata);
        var handler = new DeleteEmploymentChangeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteEmploymentChangeReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(EmploymentChangeReadinessState.Archived, stored.EmploymentChangeReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateEmploymentChangeReadinessCommand(ValidRequest(readinessState: EmploymentChangeReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmploymentChangeReadinessState.Deferred, stored.EmploymentChangeReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: EmploymentChangeReadinessState.Ready,
            dataMinimizationState: EmploymentChangeReadinessState.Ready,
            retentionPolicyState: EmploymentChangeReadinessState.Ready,
            evidencePolicyState: EmploymentChangeReadinessState.Ready);
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository(metadata);
        var handler = new EvaluateEmploymentChangeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEmploymentChangeReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmploymentChangeReadinessState.Ready, response.Data!.EmploymentChangeReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: EmploymentChangeReadinessState.Deferred);
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository(metadata);
        var handler = new EvaluateEmploymentChangeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEmploymentChangeReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmploymentChangeReadinessState.Deferred, response.Data!.EmploymentChangeReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Workflow_approval_position_and_action_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEmploymentChangeReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(changeLifecycleBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(transferBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(promotionBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(approvalBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(positionAssignmentBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(employeeActionBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(managerActionBoundaryState: EmploymentChangeReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateEmploymentChangeReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Compensation_benefits_and_payroll_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEmploymentChangeReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(compensationDataBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(benefitsDataBoundaryState: EmploymentChangeReadinessState.Ready),
            ValidRequest(payrollDataBoundaryState: EmploymentChangeReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateEmploymentChangeReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEmploymentChangeReadinessMetadataRepository(metadata);
        var response = await new GetEmploymentChangeAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmploymentChangeAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(EmploymentChangeGuard.AuditReadPermission, PermissionFor(nameof(EmploymentChangesController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("workflow_body")]
    [InlineData("approval_decision")]
    [InlineData("position_mutation")]
    [InlineData("action_payload")]
    [InlineData("manager_note")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    [InlineData("salary_amount")]
    [InlineData("payroll")]
    [InlineData("benefits_election")]
    [InlineData("tax")]
    public async Task Forbidden_workflow_approval_position_compensation_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEmploymentChangeReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateEmploymentChangeReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
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
            "ApprovalDecision",
            "PositionMutation",
            "ActionPayload",
            "ManagerNote",
            "HrNote",
            "EmployeeStatement",
            "FreeText",
            "Narrative",
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
            typeof(EmploymentChangeReadinessCreateRequest),
            typeof(EmploymentChangeReadinessDto),
            typeof(EmploymentChangeReadinessMetadata)
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
        Assert.Null(typeof(EmploymentChangeReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(EmploymentChangeGuard.ReadPermission, PermissionFor(nameof(EmploymentChangesController.GetAll)));
        Assert.Equal(EmploymentChangeGuard.ReadPermission, PermissionFor(nameof(EmploymentChangesController.GetById)));
        Assert.Equal(EmploymentChangeGuard.ManagePermission, PermissionFor(nameof(EmploymentChangesController.Create)));
        Assert.Equal(EmploymentChangeGuard.EvaluatePermission, PermissionFor(nameof(EmploymentChangesController.Evaluate)));
        Assert.Equal(EmploymentChangeGuard.ManagePermission, PermissionFor(nameof(EmploymentChangesController.Delete)));
        Assert.Equal(EmploymentChangeGuard.AuditReadPermission, PermissionFor(nameof(EmploymentChangesController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_employment_change_readiness", MongoEmploymentChangeReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_employment_change_tenant_code_active", MongoEmploymentChangeReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0026");
        var legacy = string.Join("-", "MOD", "0304");
        var runtimeStrings = new[]
        {
            EmploymentChangeGuard.OwnerKey,
            EmploymentChangeGuard.ReadPermission,
            EmploymentChangeGuard.ManagePermission,
            EmploymentChangeGuard.EvaluatePermission,
            EmploymentChangeGuard.AuditReadPermission,
            MongoEmploymentChangeReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateEmploymentChangeReadinessHandler CreateHandler(
        IEmploymentChangeReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static EmploymentChangeReadinessCreateRequest ValidRequest(
        string code = "EMPCHANGE-001",
        EmploymentChangeReadinessState readinessState = EmploymentChangeReadinessState.Draft,
        string sourceContractVersion = "v1",
        EmploymentChangeReadinessState changeLifecycleBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState transferBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState promotionBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState approvalBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState positionAssignmentBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState employeeActionBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState managerActionBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState compensationDataBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState benefitsDataBoundaryState = EmploymentChangeReadinessState.NotRequired,
        EmploymentChangeReadinessState payrollDataBoundaryState = EmploymentChangeReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = "Employment change readiness",
            EmploymentChangeReadinessState = readinessState,
            ChangeLifecycleBoundaryState = changeLifecycleBoundaryState,
            TransferBoundaryState = transferBoundaryState,
            PromotionBoundaryState = promotionBoundaryState,
            ApprovalBoundaryState = approvalBoundaryState,
            PositionAssignmentBoundaryState = positionAssignmentBoundaryState,
            EmployeeActionBoundaryState = employeeActionBoundaryState,
            ManagerActionBoundaryState = managerActionBoundaryState,
            CompensationDataBoundaryState = compensationDataBoundaryState,
            BenefitsDataBoundaryState = benefitsDataBoundaryState,
            PayrollDataBoundaryState = payrollDataBoundaryState,
            DocumentDependencyState = EmploymentChangeReadinessState.NotRequired,
            NotificationDependencyState = EmploymentChangeReadinessState.NotRequired,
            ConsentPreconditionState = EmploymentChangeReadinessState.Deferred,
            DataMinimizationState = EmploymentChangeReadinessState.Deferred,
            RetentionPolicyState = EmploymentChangeReadinessState.Deferred,
            EvidencePolicyState = EmploymentChangeReadinessState.Deferred,
            DependencyStates = new Dictionary<string, EmploymentChangeReadinessState>
            {
                ["employeeProjection"] = EmploymentChangeReadinessState.Ready,
                ["positionOverlay"] = EmploymentChangeReadinessState.Ready,
                ["employeeOnboarding"] = EmploymentChangeReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            EmploymentChangeReadinessVersion = 1
        };

    private static EmploymentChangeReadinessMetadata Metadata(
        Guid tenantId,
        string code = "EMPCHANGE-001",
        EmploymentChangeReadinessState consentPreconditionState = EmploymentChangeReadinessState.Deferred,
        EmploymentChangeReadinessState dataMinimizationState = EmploymentChangeReadinessState.Deferred,
        EmploymentChangeReadinessState retentionPolicyState = EmploymentChangeReadinessState.Deferred,
        EmploymentChangeReadinessState evidencePolicyState = EmploymentChangeReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Employment change readiness",
            EmploymentChangeReadinessState = EmploymentChangeReadinessState.Draft,
            ChangeLifecycleBoundaryState = EmploymentChangeReadinessState.NotRequired,
            TransferBoundaryState = EmploymentChangeReadinessState.NotRequired,
            PromotionBoundaryState = EmploymentChangeReadinessState.NotRequired,
            ApprovalBoundaryState = EmploymentChangeReadinessState.NotRequired,
            PositionAssignmentBoundaryState = EmploymentChangeReadinessState.NotRequired,
            EmployeeActionBoundaryState = EmploymentChangeReadinessState.NotRequired,
            ManagerActionBoundaryState = EmploymentChangeReadinessState.NotRequired,
            CompensationDataBoundaryState = EmploymentChangeReadinessState.NotRequired,
            BenefitsDataBoundaryState = EmploymentChangeReadinessState.NotRequired,
            PayrollDataBoundaryState = EmploymentChangeReadinessState.NotRequired,
            DocumentDependencyState = EmploymentChangeReadinessState.NotRequired,
            NotificationDependencyState = EmploymentChangeReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, EmploymentChangeReadinessState>
            {
                ["employeeOnboarding"] = EmploymentChangeReadinessState.Ready
            },
            SourceContractVersion = "v1",
            EmploymentChangeReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, EmploymentChangeReadinessMetadata> RepositoryItems(
        InMemoryEmploymentChangeReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryEmploymentChangeReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EmploymentChangeReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(EmploymentChangesController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryEmploymentChangeReadinessMetadataRepository : IEmploymentChangeReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, EmploymentChangeReadinessMetadata> _items;

        public InMemoryEmploymentChangeReadinessMetadataRepository(params EmploymentChangeReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EmploymentChangeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmploymentChangeReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<EmploymentChangeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
