using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OffboardingCases;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Handlers;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class OffboardingCaseTests
{
    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var repository = new InMemoryOffboardingCaseRepository();
        var handler = CreateHandler(repository, new InMemoryEmployeeProjectionRepository(employee), new InMemoryPositionAssignmentRepository(), DataScopeEvaluator.Allowed(), tenantId);

        var first = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(employee.Id, code: "exit-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(employee.Id, code: " EXIT-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var employee = EmployeeProjection(tenantA);
        var offboardingCase = Case(tenantA, employee.Id);
        var handler = new GetOffboardingCaseByIdHandler(
            new InMemoryOffboardingCaseRepository(offboardingCase),
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetOffboardingCaseByIdQuery(offboardingCase.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Get_all_filters_sensitive_and_restricted_cases_from_broad_read()
    {
        var tenantId = Guid.NewGuid();
        var standardEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.StandardHr, "EMP-001");
        var sensitiveEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.SensitiveHr, "EMP-002");
        var restrictedEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.RestrictedHr, "EMP-003");
        var standardCase = Case(tenantId, standardEmployee.Id, "EXIT-001");
        var sensitiveCase = Case(tenantId, sensitiveEmployee.Id, "EXIT-002");
        var restrictedCase = Case(tenantId, restrictedEmployee.Id, "EXIT-003");
        var handler = new GetOffboardingCaseListHandler(
            new InMemoryOffboardingCaseRepository(standardCase, sensitiveCase, restrictedCase),
            new InMemoryEmployeeProjectionRepository(standardEmployee, sensitiveEmployee, restrictedEmployee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(new GetOffboardingCaseListQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var row = Assert.Single(response.Data!);
        Assert.Equal(standardCase.Id, row.Id);
    }

    [Fact]
    public async Task Get_by_id_fails_closed_for_sensitive_case_with_broad_read()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.SensitiveHr);
        var offboardingCase = Case(tenantId, employee.Id);
        var handler = new GetOffboardingCaseByIdHandler(
            new InMemoryOffboardingCaseRepository(offboardingCase),
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(new GetOffboardingCaseByIdQuery(offboardingCase.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var offboardingCase = Case(tenantId, Guid.NewGuid());
        var repository = new InMemoryOffboardingCaseRepository(offboardingCase);
        var handler = new ArchiveOffboardingCaseHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new ArchiveOffboardingCaseCommand(offboardingCase.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, offboardingCase.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[offboardingCase.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(OffboardingState.Archived, stored.OffboardingState);
    }

    [Fact]
    public async Task Missing_employee_projection_anchor_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(Guid.NewGuid())), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_employee_projection_anchor_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(Guid.NewGuid());
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(employee.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Sensitive_access_precondition_blocks_mutation()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Denied(),
            tenantId);

        var response = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(employee.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Assignment_overlay_context_is_optional_and_records_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var repository = new InMemoryOffboardingCaseRepository();
        var handler = CreateHandler(
            repository,
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(new CreateOffboardingCaseCommand(ValidRequest(employee.Id, state: OffboardingState.Approved)), CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(OffboardingState.ReviewRequired, stored.OffboardingState);
        Assert.Contains("Assignment overlay context was not supplied.", stored.DeferredReason);
    }

    [Fact]
    public async Task Missing_assignment_overlay_context_fails_closed_when_supplied()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreateOffboardingCaseCommand(ValidRequest(employee.Id, assignmentOverlayId: Guid.NewGuid())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Workflow_audit_evidence_retention_deferred_metadata_is_preserved()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var repository = new InMemoryOffboardingCaseRepository();
        var handler = CreateHandler(
            repository,
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreateOffboardingCaseCommand(ValidRequest(employee.Id, dependencyState: OffboardingDependencyDecisionState.Deferred)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(OffboardingDependencyDecisionState.Deferred, stored.DependencyDecisionState);
        Assert.Contains("dependency metadata is deferred", stored.DeferredReason);
    }

    [Fact]
    public async Task Handoff_metadata_is_local_only_and_rejects_transferred_state()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var offboardingCase = Case(tenantId, employee.Id);
        var repository = new InMemoryOffboardingCaseRepository(offboardingCase);
        var handler = new PlanOffboardingHandoffHandler(
            repository,
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var planned = await handler.Handle(
            new PlanOffboardingHandoffCommand(
                offboardingCase.Id,
                new OffboardingCaseHandoffRequest
                {
                    TepHandoffState = OffboardingTepHandoffState.Deferred,
                    TepHandoffReferenceKey = "local-handoff-001",
                    SourceContractVersion = "v1",
                    OffboardingVersion = 2
                }),
            CancellationToken.None);
        var transferred = await handler.Handle(
            new PlanOffboardingHandoffCommand(
                offboardingCase.Id,
                new OffboardingCaseHandoffRequest
                {
                    TepHandoffState = OffboardingTepHandoffState.Transferred,
                    SourceContractVersion = "v1",
                    OffboardingVersion = 3
                }),
            CancellationToken.None);

        Assert.True(planned.IsSuccessful);
        Assert.Equal(OffboardingTepHandoffState.Deferred, planned.Data!.TepHandoffState);
        Assert.Equal("local-handoff-001", planned.Data.TepHandoffReferenceKey);
        Assert.False(transferred.IsSuccessful);
        Assert.Equal(400, transferred.StatusCode);
    }

    [Fact]
    public async Task Review_cannot_mark_handoff_ready_without_existing_local_handoff_metadata()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var offboardingCase = Case(tenantId, employee.Id);
        offboardingCase.TepHandoffState = OffboardingTepHandoffState.NotRequired;
        offboardingCase.TepHandoffReferenceKey = null;
        var handler = new ReviewOffboardingCaseHandler(
            new InMemoryOffboardingCaseRepository(offboardingCase),
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(
            new ReviewOffboardingCaseCommand(
                offboardingCase.Id,
                new OffboardingCaseReviewRequest
                {
                    OffboardingState = OffboardingState.TepHandoffReady,
                    DependencyDecisionState = OffboardingDependencyDecisionState.Deferred,
                    SourceContractVersion = "v1",
                    OffboardingVersion = 2
                }),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData(OffboardingTepHandoffState.Planned)]
    [InlineData(OffboardingTepHandoffState.Ready)]
    [InlineData(OffboardingTepHandoffState.Deferred)]
    public async Task Review_can_mark_handoff_ready_with_existing_local_handoff_metadata(OffboardingTepHandoffState handoffState)
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var offboardingCase = Case(tenantId, employee.Id);
        offboardingCase.TepHandoffState = handoffState;
        offboardingCase.TepHandoffReferenceKey = "local-handoff-001";
        var repository = new InMemoryOffboardingCaseRepository(offboardingCase);
        var handler = new ReviewOffboardingCaseHandler(
            repository,
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(
            new ReviewOffboardingCaseCommand(
                offboardingCase.Id,
                new OffboardingCaseReviewRequest
                {
                    OffboardingState = OffboardingState.TepHandoffReady,
                    DependencyDecisionState = OffboardingDependencyDecisionState.Deferred,
                    SourceContractVersion = "v1",
                    OffboardingVersion = 2
                }),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(OffboardingState.TepHandoffReady, response.Data!.OffboardingState);
        Assert.Equal(handoffState, response.Data.TepHandoffState);
    }

    [Fact]
    public async Task Raw_sensitive_markers_are_rejected()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreateOffboardingCaseCommand(ValidRequest(employee.Id, sourceContractVersion: "v1 access_token")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Completed_state_requires_completed_checklist_and_actual_exit_date()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryOffboardingCaseRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            new InMemoryPositionAssignmentRepository(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreateOffboardingCaseCommand(ValidRequest(employee.Id, state: OffboardingState.Completed)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(OffboardingCaseGuard.ReadPermission, PermissionFor(nameof(OffboardingCasesController.GetAll)));
        Assert.Equal(OffboardingCaseGuard.ReadPermission, PermissionFor(nameof(OffboardingCasesController.GetById)));
        Assert.Equal(OffboardingCaseGuard.ManagePermission, PermissionFor(nameof(OffboardingCasesController.Create)));
        Assert.Equal(OffboardingCaseGuard.ManagePermission, PermissionFor(nameof(OffboardingCasesController.Update)));
        Assert.Equal(OffboardingCaseGuard.ReviewPermission, PermissionFor(nameof(OffboardingCasesController.Review)));
        Assert.Equal(OffboardingCaseGuard.HandoffManagePermission, PermissionFor(nameof(OffboardingCasesController.PlanHandoff)));
        Assert.Equal(OffboardingCaseGuard.ArchivePermission, PermissionFor(nameof(OffboardingCasesController.Archive)));
        Assert.Equal(OffboardingCaseGuard.ReadPermission, PermissionFor(nameof(OffboardingCasesController.GetHealth)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_offboarding_cases", MongoOffboardingCaseRepository.CollectionName);
        Assert.Equal("ux_hcm_offboarding_cases_tenant_code_active", MongoOffboardingCaseRepository.ActiveCodeUniqueIndexName);
    }

    private static CreateOffboardingCaseHandler CreateHandler(
        IOffboardingCaseRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        IPositionAssignmentOverlayRepository assignmentRepository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        Guid tenantId) =>
        new(repository, employeeRepository, assignmentRepository, dataScopeEvaluator, new FixedTenantContext(tenantId));

    private static OffboardingCaseCreateRequest ValidRequest(
        Guid employeeProjectionId,
        Guid? assignmentOverlayId = null,
        string code = "EXIT-001",
        OffboardingState state = OffboardingState.Draft,
        OffboardingDependencyDecisionState dependencyState = OffboardingDependencyDecisionState.Deferred,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            EmployeeProjectionId = employeeProjectionId,
            AssignmentOverlayId = assignmentOverlayId,
            ExitReasonCode = "VOLUNTARY",
            ExitTypeCode = "RESIGNATION",
            NoticeDate = DateTimeOffset.UtcNow,
            PlannedExitDate = DateTimeOffset.UtcNow.AddDays(14),
            OffboardingState = state,
            ChecklistState = state == OffboardingState.Completed ? OffboardingChecklistState.Completed : OffboardingChecklistState.Planned,
            DependencyDecisionState = dependencyState,
            TepHandoffState = OffboardingTepHandoffState.Deferred,
            SourceContractVersion = sourceContractVersion,
            OffboardingVersion = 1
        };

    private static EmployeeProfileProjection EmployeeProjection(
        Guid tenantId,
        EmployeeVisibilityClassification visibility = EmployeeVisibilityClassification.StandardHr,
        string code = "EMP-001") =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            HrisSourceProfileId = Guid.NewGuid(),
            PersonReferenceId = Guid.NewGuid(),
            ExternalEmployeeReference = $"external-{code}",
            EmploymentRecordReferenceKey = $"employment-{code}",
            EmploymentStatusCode = "ACTIVE",
            WorkerTypeCode = "EMPLOYEE",
            SourceContractVersion = "v1",
            ProjectionState = EmployeeProjectionState.Validated,
            VisibilityClassification = visibility,
            ProjectionVersion = 1
        };

    private static OffboardingCase Case(Guid tenantId, Guid employeeProjectionId, string code = "EXIT-001") =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            EmployeeProjectionId = employeeProjectionId,
            ExitReasonCode = "VOLUNTARY",
            ExitTypeCode = "RESIGNATION",
            PlannedExitDate = DateTimeOffset.UtcNow.AddDays(14),
            OffboardingState = OffboardingState.Draft,
            ChecklistState = OffboardingChecklistState.Planned,
            SensitiveAccessDecisionState = OffboardingSensitiveAccessDecisionState.Allowed,
            DependencyDecisionState = OffboardingDependencyDecisionState.Deferred,
            TepHandoffState = OffboardingTepHandoffState.Deferred,
            SourceContractVersion = "v1",
            OffboardingVersion = 1
        };

    private static IReadOnlyDictionary<Guid, OffboardingCase> RepositoryItems(InMemoryOffboardingCaseRepository repository)
    {
        var field = typeof(InMemoryOffboardingCaseRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, OffboardingCase>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(OffboardingCasesController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class DataScopeEvaluator : ISensitiveAccessDataScopeEvaluator
    {
        private readonly SensitiveAccessDataScopeResult _result;

        private DataScopeEvaluator(SensitiveAccessDataScopeResult result) => _result = result;

        public static DataScopeEvaluator Allowed() => new(SensitiveAccessDataScopeResult.Allowed());

        public static DataScopeEvaluator Denied() => new(SensitiveAccessDataScopeResult.OutOfScope());

        public static DataScopeEvaluator Unavailable() => new(SensitiveAccessDataScopeResult.ContractUnavailable());

        public Task<SensitiveAccessDataScopeResult> EvaluateAsync(
            Guid tenantId,
            EmployeeProfileProjection projection,
            CancellationToken ct) =>
            Task.FromResult(_result);
    }

    private sealed class InMemoryEmployeeProjectionRepository : IEmployeeProjectionRepository
    {
        private readonly Dictionary<Guid, EmployeeProfileProjection> _items;

        public InMemoryEmployeeProjectionRepository(params EmployeeProfileProjection[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmployeeProfileProjection>>(_items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());

        public Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPositionAssignmentRepository : IPositionAssignmentOverlayRepository
    {
        private readonly Dictionary<Guid, EmployeePositionAssignmentOverlay> _items;

        public InMemoryPositionAssignmentRepository(params EmployeePositionAssignmentOverlay[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<EmployeePositionAssignmentOverlay>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmployeePositionAssignmentOverlay>>(_items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());

        public Task<EmployeePositionAssignmentOverlay?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
        {
            _items[assignment.Id] = assignment;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
        {
            _items[assignment.Id] = assignment;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryOffboardingCaseRepository : IOffboardingCaseRepository
    {
        private readonly Dictionary<Guid, OffboardingCase> _items;

        public InMemoryOffboardingCaseRepository(params OffboardingCase[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<OffboardingCase>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OffboardingCase>>(_items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());

        public Task<OffboardingCase?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(OffboardingCase offboardingCase, CancellationToken ct)
        {
            _items[offboardingCase.Id] = offboardingCase;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OffboardingCase offboardingCase, CancellationToken ct)
        {
            _items[offboardingCase.Id] = offboardingCase;
            return Task.CompletedTask;
        }
    }
}
