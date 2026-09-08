using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Handlers;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class EmployeeOnboardingTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateEmployeeOnboardingReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetEmployeeOnboardingReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmployeeOnboardingReadinessListQuery(), CancellationToken.None);
        var get = await new GetEmployeeOnboardingReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmployeeOnboardingReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("ONBOARDING-001", get.Data!.Code);
        Assert.Equal("Employee onboarding readiness", get.Data.DisplayName);
        Assert.Equal(EmployeeOnboardingReadinessState.NotRequired, get.Data.LifecycleBoundaryState);
        Assert.Equal(EmployeeOnboardingReadinessState.NotRequired, get.Data.ChecklistBoundaryState);
        Assert.Equal(EmployeeOnboardingReadinessState.NotRequired, get.Data.ManagerActionBoundaryState);
        Assert.Equal(EmployeeOnboardingReadinessState.NotRequired, get.Data.EmployeeActionBoundaryState);
        Assert.Equal(EmployeeOnboardingReadinessState.Deferred, get.Data.IdentityProvisioningBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetEmployeeOnboardingReadinessByIdHandler(
            new InMemoryEmployeeOnboardingReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetEmployeeOnboardingReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryEmployeeOnboardingReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateEmployeeOnboardingReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateEmployeeOnboardingReadinessCommand(ValidRequest(code: "onboarding-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateEmployeeOnboardingReadinessCommand(ValidRequest(code: " ONBOARDING-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository(metadata);
        var handler = new DeleteEmployeeOnboardingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteEmployeeOnboardingReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(EmployeeOnboardingReadinessState.Archived, stored.OnboardingReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateEmployeeOnboardingReadinessCommand(ValidRequest(onboardingState: EmployeeOnboardingReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmployeeOnboardingReadinessState.Deferred, stored.OnboardingReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            lifecycleBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            checklistBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            managerActionBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            employeeActionBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            candidateTransitionBoundaryState: EmployeeOnboardingReadinessState.Ready,
            identityProvisioningBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            accessProvisioningBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            deviceEquipmentProvisioningBoundaryState: EmployeeOnboardingReadinessState.NotRequired,
            consentPreconditionState: EmployeeOnboardingReadinessState.Ready,
            dataMinimizationState: EmployeeOnboardingReadinessState.Ready,
            retentionPolicyState: EmployeeOnboardingReadinessState.Ready,
            evidencePolicyState: EmployeeOnboardingReadinessState.Ready,
            notificationDependencyState: EmployeeOnboardingReadinessState.NotRequired,
            documentDependencyState: EmployeeOnboardingReadinessState.NotRequired);
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository(metadata);
        var handler = new EvaluateEmployeeOnboardingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEmployeeOnboardingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmployeeOnboardingReadinessState.Ready, response.Data!.OnboardingReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: EmployeeOnboardingReadinessState.Deferred);
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository(metadata);
        var handler = new EvaluateEmployeeOnboardingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateEmployeeOnboardingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(EmployeeOnboardingReadinessState.Deferred, response.Data!.OnboardingReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryEmployeeOnboardingReadinessMetadataRepository(metadata);
        var response = await new GetEmployeeOnboardingAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetEmployeeOnboardingAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(EmployeeOnboardingGuard.AuditReadPermission, PermissionFor(nameof(EmployeeOnboardingController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("task_body")]
    [InlineData("checklist_payload")]
    [InlineData("action_payload")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provisioning_secret")]
    [InlineData("payroll")]
    [InlineData("bank")]
    [InlineData("tax")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    public async Task Forbidden_workflow_provisioning_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryEmployeeOnboardingReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateEmployeeOnboardingReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_workflow_payload_and_sensitive_fields()
    {
        var forbiddenFragments = new[]
        {
            "TaskBody",
            "ChecklistPayload",
            "ActionPayload",
            "ManagerNote",
            "HrNote",
            "EmployeeNote",
            "FreeText",
            "Narrative",
            "Amount",
            "Salary",
            "Wage",
            "Bank",
            "Tax",
            "PayrollDetail",
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
            typeof(EmployeeOnboardingReadinessCreateRequest),
            typeof(EmployeeOnboardingReadinessDto),
            typeof(EmployeeOnboardingReadinessMetadata)
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
        Assert.Null(typeof(EmployeeOnboardingReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(EmployeeOnboardingGuard.ReadPermission, PermissionFor(nameof(EmployeeOnboardingController.GetAll)));
        Assert.Equal(EmployeeOnboardingGuard.ReadPermission, PermissionFor(nameof(EmployeeOnboardingController.GetById)));
        Assert.Equal(EmployeeOnboardingGuard.ManagePermission, PermissionFor(nameof(EmployeeOnboardingController.Create)));
        Assert.Equal(EmployeeOnboardingGuard.EvaluatePermission, PermissionFor(nameof(EmployeeOnboardingController.Evaluate)));
        Assert.Equal(EmployeeOnboardingGuard.ManagePermission, PermissionFor(nameof(EmployeeOnboardingController.Delete)));
        Assert.Equal(EmployeeOnboardingGuard.AuditReadPermission, PermissionFor(nameof(EmployeeOnboardingController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_employee_onboarding_readiness", MongoEmployeeOnboardingReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_employee_onboarding_tenant_code_active", MongoEmployeeOnboardingReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0025");
        var legacy = string.Join("-", "MOD", "0303");
        var runtimeStrings = new[]
        {
            EmployeeOnboardingGuard.OwnerKey,
            EmployeeOnboardingGuard.ReadPermission,
            EmployeeOnboardingGuard.ManagePermission,
            EmployeeOnboardingGuard.EvaluatePermission,
            EmployeeOnboardingGuard.AuditReadPermission,
            MongoEmployeeOnboardingReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateEmployeeOnboardingReadinessHandler CreateHandler(
        IEmployeeOnboardingReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static EmployeeOnboardingReadinessCreateRequest ValidRequest(
        string code = "ONBOARDING-001",
        EmployeeOnboardingReadinessState onboardingState = EmployeeOnboardingReadinessState.Draft,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            DisplayName = "Employee onboarding readiness",
            OnboardingReadinessState = onboardingState,
            LifecycleBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
            ChecklistBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
            ManagerActionBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
            EmployeeActionBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
            CandidateTransitionBoundaryState = EmployeeOnboardingReadinessState.Deferred,
            IdentityProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
            AccessProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
            DeviceEquipmentProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
            DocumentDependencyState = EmployeeOnboardingReadinessState.NotRequired,
            NotificationDependencyState = EmployeeOnboardingReadinessState.NotRequired,
            ConsentPreconditionState = EmployeeOnboardingReadinessState.Deferred,
            DataMinimizationState = EmployeeOnboardingReadinessState.Deferred,
            RetentionPolicyState = EmployeeOnboardingReadinessState.Deferred,
            EvidencePolicyState = EmployeeOnboardingReadinessState.Deferred,
            DependencyStates = new Dictionary<string, EmployeeOnboardingReadinessState>
            {
                ["employeeProjection"] = EmployeeOnboardingReadinessState.Ready,
                ["sensitiveAccess"] = EmployeeOnboardingReadinessState.Ready,
                ["candidatePipeline"] = EmployeeOnboardingReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            OnboardingReadinessVersion = 1
        };

    private static EmployeeOnboardingReadinessMetadata Metadata(
        Guid tenantId,
        string code = "ONBOARDING-001",
        EmployeeOnboardingReadinessState lifecycleBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
        EmployeeOnboardingReadinessState checklistBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
        EmployeeOnboardingReadinessState managerActionBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
        EmployeeOnboardingReadinessState employeeActionBoundaryState = EmployeeOnboardingReadinessState.NotRequired,
        EmployeeOnboardingReadinessState candidateTransitionBoundaryState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState identityProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState accessProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState deviceEquipmentProvisioningBoundaryState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState consentPreconditionState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState dataMinimizationState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState retentionPolicyState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState evidencePolicyState = EmployeeOnboardingReadinessState.Deferred,
        EmployeeOnboardingReadinessState notificationDependencyState = EmployeeOnboardingReadinessState.NotRequired,
        EmployeeOnboardingReadinessState documentDependencyState = EmployeeOnboardingReadinessState.NotRequired) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Employee onboarding readiness",
            OnboardingReadinessState = EmployeeOnboardingReadinessState.Draft,
            LifecycleBoundaryState = lifecycleBoundaryState,
            ChecklistBoundaryState = checklistBoundaryState,
            ManagerActionBoundaryState = managerActionBoundaryState,
            EmployeeActionBoundaryState = employeeActionBoundaryState,
            CandidateTransitionBoundaryState = candidateTransitionBoundaryState,
            IdentityProvisioningBoundaryState = identityProvisioningBoundaryState,
            AccessProvisioningBoundaryState = accessProvisioningBoundaryState,
            DeviceEquipmentProvisioningBoundaryState = deviceEquipmentProvisioningBoundaryState,
            DocumentDependencyState = documentDependencyState,
            NotificationDependencyState = notificationDependencyState,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, EmployeeOnboardingReadinessState>
            {
                ["candidatePipeline"] = EmployeeOnboardingReadinessState.Ready
            },
            SourceContractVersion = "v1",
            OnboardingReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, EmployeeOnboardingReadinessMetadata> RepositoryItems(
        InMemoryEmployeeOnboardingReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryEmployeeOnboardingReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EmployeeOnboardingReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(EmployeeOnboardingController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryEmployeeOnboardingReadinessMetadataRepository : IEmployeeOnboardingReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, EmployeeOnboardingReadinessMetadata> _items;

        public InMemoryEmployeeOnboardingReadinessMetadataRepository(params EmployeeOnboardingReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EmployeeOnboardingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmployeeOnboardingReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<EmployeeOnboardingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
