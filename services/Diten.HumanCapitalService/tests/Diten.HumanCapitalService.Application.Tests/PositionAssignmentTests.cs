using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class PositionAssignmentTests
{
    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var repository = new InMemoryPositionAssignmentRepository();
        var employeeRepository = new InMemoryEmployeeProjectionRepository(employee);
        var handler = CreateHandler(repository, employeeRepository, ValidReferences(), DataScopeEvaluator.Allowed(), tenantId);

        var first = await handler.Handle(new CreatePositionAssignmentCommand(ValidRequest(employee.Id, code: "assign-001")), CancellationToken.None);
        var second = await handler.Handle(new CreatePositionAssignmentCommand(ValidRequest(employee.Id, code: " ASSIGN-001 ")), CancellationToken.None);

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
        var assignment = Assignment(tenantA, employee.Id);
        var get = new GetPositionAssignmentByIdHandler(
            new InMemoryPositionAssignmentRepository(assignment),
            new InMemoryEmployeeProjectionRepository(employee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantB));

        var response = await get.Handle(new GetPositionAssignmentByIdQuery(assignment.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Get_all_filters_sensitive_and_restricted_assignment_metadata_from_broad_read()
    {
        var tenantId = Guid.NewGuid();
        var standardEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.StandardHr, "EMP-001");
        var sensitiveEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.SensitiveHr, "EMP-002");
        var restrictedEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.RestrictedHr, "EMP-003");
        var standardAssignment = Assignment(tenantId, standardEmployee.Id, "ASSIGN-001");
        var sensitiveAssignment = Assignment(tenantId, sensitiveEmployee.Id, "ASSIGN-002");
        var restrictedAssignment = Assignment(tenantId, restrictedEmployee.Id, "ASSIGN-003");
        var handler = new GetPositionAssignmentListHandler(
            new InMemoryPositionAssignmentRepository(standardAssignment, sensitiveAssignment, restrictedAssignment),
            new InMemoryEmployeeProjectionRepository(standardEmployee, sensitiveEmployee, restrictedEmployee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(new GetPositionAssignmentListQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        var row = Assert.Single(response.Data!);
        Assert.Equal(standardAssignment.Id, row.Id);
    }

    [Fact]
    public async Task Get_by_id_fails_closed_for_sensitive_assignment_metadata_with_broad_read()
    {
        var tenantId = Guid.NewGuid();
        var sensitiveEmployee = EmployeeProjection(tenantId, EmployeeVisibilityClassification.SensitiveHr);
        var assignment = Assignment(tenantId, sensitiveEmployee.Id);
        var handler = new GetPositionAssignmentByIdHandler(
            new InMemoryPositionAssignmentRepository(assignment),
            new InMemoryEmployeeProjectionRepository(sensitiveEmployee),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var response = await handler.Handle(new GetPositionAssignmentByIdQuery(assignment.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var assignment = Assignment(tenantId, Guid.NewGuid());
        var repository = new InMemoryPositionAssignmentRepository(assignment);
        var archive = new ArchivePositionAssignmentHandler(repository, new FixedTenantContext(tenantId));

        var response = await archive.Handle(new ArchivePositionAssignmentCommand(assignment.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, assignment.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[assignment.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(AssignmentOverlayState.Archived, stored.AssignmentState);
    }

    [Fact]
    public async Task Missing_employee_projection_anchor_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(),
            ValidReferences(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(Guid.NewGuid())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_employee_projection_anchor_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(Guid.NewGuid());
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            ValidReferences(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Sensitive_access_precondition_blocks_assignment_mutation()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            ValidReferences(),
            DataScopeEvaluator.Denied(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Unavailable_pss_contract_defers_non_validated_state()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            UnavailableReferences(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id, state: AssignmentOverlayState.Deferred)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Unavailable_pss_contract_fails_closed_for_validated_state()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            UnavailableReferences(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id, state: AssignmentOverlayState.Validated)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_pss_reference_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            MissingPersonReference(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id, state: AssignmentOverlayState.Validated)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Raw_sensitive_markers_are_rejected()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var handler = CreateHandler(
            new InMemoryPositionAssignmentRepository(),
            new InMemoryEmployeeProjectionRepository(employee),
            ValidReferences(),
            DataScopeEvaluator.Allowed(),
            tenantId);

        var response = await handler.Handle(
            new CreatePositionAssignmentCommand(ValidRequest(employee.Id, sourceContractVersion: "v1_access_token")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Reference_link_handler_updates_only_pss_reference_fields()
    {
        var tenantId = Guid.NewGuid();
        var employee = EmployeeProjection(tenantId);
        var originalManagerId = Guid.NewGuid();
        var originalEffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10);
        var assignment = Assignment(tenantId, employee.Id);
        assignment.ManagerEmployeeProjectionId = originalManagerId;
        assignment.EffectiveFrom = originalEffectiveFrom;
        assignment.EffectiveTo = originalEffectiveFrom.AddDays(30);
        assignment.AssignmentState = AssignmentOverlayState.Active;
        assignment.AssignmentVersion = 7;
        var manager = EmployeeProjection(tenantId, EmployeeVisibilityClassification.StandardHr, "MANAGER");
        manager.Id = originalManagerId;
        var repository = new InMemoryPositionAssignmentRepository(assignment);
        var handler = new UpdatePositionAssignmentReferenceLinkHandler(
            repository,
            new InMemoryEmployeeProjectionRepository(employee, manager),
            ValidReferences(),
            DataScopeEvaluator.Allowed(),
            new FixedTenantContext(tenantId));

        var newPerson = Guid.NewGuid();
        var newOrg = Guid.NewGuid();
        var newPosition = Guid.NewGuid();
        var response = await handler.Handle(
            new UpdatePositionAssignmentReferenceLinkCommand(
                assignment.Id,
                new PositionAssignmentReferenceLinkRequest
                {
                    PersonReferenceId = newPerson,
                    OrganizationUnitId = newOrg,
                    PositionId = newPosition,
                    SourceContractVersion = "v2"
                }),
            CancellationToken.None);

        var stored = RepositoryItems(repository)[assignment.Id];
        Assert.True(response.IsSuccessful);
        Assert.Equal(newPerson, stored.PersonReferenceId);
        Assert.Equal(newOrg, stored.OrganizationUnitId);
        Assert.Equal(newPosition, stored.PositionId);
        Assert.Equal(originalManagerId, stored.ManagerEmployeeProjectionId);
        Assert.Equal(originalEffectiveFrom, stored.EffectiveFrom);
        Assert.Equal(originalEffectiveFrom.AddDays(30), stored.EffectiveTo);
        Assert.Equal(AssignmentOverlayState.Active, stored.AssignmentState);
        Assert.Equal(7, stored.AssignmentVersion);
    }

    [Fact]
    public void Reference_link_request_exposes_only_pss_reference_link_fields()
    {
        var properties = typeof(PositionAssignmentReferenceLinkRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(PositionAssignmentReferenceLinkRequest.PersonReferenceId),
                nameof(PositionAssignmentReferenceLinkRequest.OrganizationUnitId),
                nameof(PositionAssignmentReferenceLinkRequest.PositionId),
                nameof(PositionAssignmentReferenceLinkRequest.SourceContractVersion)
            },
            properties);

        Assert.DoesNotContain("Code", properties);
        Assert.DoesNotContain("EmployeeProjectionId", properties);
        Assert.DoesNotContain("ManagerEmployeeProjectionId", properties);
        Assert.DoesNotContain("EffectiveFrom", properties);
        Assert.DoesNotContain("EffectiveTo", properties);
        Assert.DoesNotContain("AssignmentState", properties);
        Assert.DoesNotContain("AssignmentVersion", properties);
    }

    [Fact]
    public void Controller_uses_approved_permission_namespace()
    {
        Assert.Equal(PositionAssignmentGuard.ReadPermission, PermissionFor(nameof(PositionAssignmentsController.GetAll)));
        Assert.Equal(PositionAssignmentGuard.ReadPermission, PermissionFor(nameof(PositionAssignmentsController.GetById)));
        Assert.Equal(PositionAssignmentGuard.ManagePermission, PermissionFor(nameof(PositionAssignmentsController.Create)));
        Assert.Equal(PositionAssignmentGuard.ManagePermission, PermissionFor(nameof(PositionAssignmentsController.Update)));
        Assert.Equal(PositionAssignmentGuard.ReferenceLinkManagePermission, PermissionFor(nameof(PositionAssignmentsController.UpdateReferenceLink)));
        Assert.Equal(PositionAssignmentGuard.ArchivePermission, PermissionFor(nameof(PositionAssignmentsController.Archive)));
        Assert.Equal(PositionAssignmentGuard.ReadPermission, PermissionFor(nameof(PositionAssignmentsController.GetHealth)));
    }

    [Fact]
    public void Mongo_repository_defines_collection_and_tenant_code_unique_index()
    {
        Assert.Equal("hcm_position_assignment_overlays", MongoPositionAssignmentOverlayRepository.CollectionName);
        Assert.Equal("ux_hcm_position_assignments_tenant_code_active", MongoPositionAssignmentOverlayRepository.ActiveCodeUniqueIndexName);
    }

    private static CreatePositionAssignmentHandler CreateHandler(
        IPositionAssignmentOverlayRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        IPositionAssignmentReferenceValidator referenceValidator,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        Guid tenantId) =>
        new(repository, employeeRepository, referenceValidator, dataScopeEvaluator, new FixedTenantContext(tenantId));

    private static PositionAssignmentCreateRequest ValidRequest(
        Guid employeeProjectionId,
        string code = "ASSIGN-001",
        AssignmentOverlayState state = AssignmentOverlayState.Validated,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            EmployeeProjectionId = employeeProjectionId,
            PersonReferenceId = Guid.NewGuid(),
            OrganizationUnitId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            EffectiveFrom = DateTimeOffset.UtcNow,
            AssignmentState = state,
            SourceContractVersion = sourceContractVersion,
            AssignmentVersion = 1
        };

    private static EmployeeProfileProjection EmployeeProjection(
        Guid tenantId,
        EmployeeVisibilityClassification visibility = EmployeeVisibilityClassification.StandardHr,
        string code = "EMP-001") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            DisplayName = "Employee Routing Label",
            HrisSourceProfileId = Guid.NewGuid(),
            PersonReferenceId = Guid.NewGuid(),
            ExternalEmployeeReference = "EXT-001",
            EmploymentRecordReferenceKey = "EMPLOYMENT-001",
            EmploymentStatusCode = "ACTIVE",
            WorkerTypeCode = "EMPLOYEE",
            SourceContractVersion = "v1",
            ProjectionState = EmployeeProjectionState.Validated,
            VisibilityClassification = visibility,
            ProjectionVersion = 1
        };

    private static EmployeePositionAssignmentOverlay Assignment(
        Guid tenantId,
        Guid employeeProjectionId,
        string code = "ASSIGN-001") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            EmployeeProjectionId = employeeProjectionId,
            PersonReferenceId = Guid.NewGuid(),
            EffectiveFrom = DateTimeOffset.UtcNow,
            AssignmentState = AssignmentOverlayState.Validated,
            SourceContractVersion = "v1",
            ReferenceValidationState = AssignmentReferenceValidationState.Validated,
            SensitiveAccessDecisionState = AssignmentSensitiveAccessDecisionState.Allowed,
            AssignmentVersion = 1
        };

    private static string PermissionFor(string methodName)
    {
        var method = typeof(PositionAssignmentsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        return (string)typeof(HasPermissionAttribute)
            .GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(attribute)!;
    }

    private static IPositionAssignmentReferenceValidator ValidReferences() =>
        new ReferenceValidator(ReferenceValidationResult.Valid());

    private static IPositionAssignmentReferenceValidator UnavailableReferences() =>
        new ReferenceValidator(ReferenceValidationResult.ContractUnavailable());

    private static IPositionAssignmentReferenceValidator MissingPersonReference() =>
        new ReferenceValidator(ReferenceValidationResult.NotFound());

    private static IReadOnlyDictionary<Guid, EmployeePositionAssignmentOverlay> RepositoryItems(InMemoryPositionAssignmentRepository repository)
    {
        var field = typeof(InMemoryPositionAssignmentRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EmployeePositionAssignmentOverlay>)field.GetValue(repository)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class ReferenceValidator : IPositionAssignmentReferenceValidator
    {
        private readonly ReferenceValidationResult _result;

        public ReferenceValidator(ReferenceValidationResult result) => _result = result;

        public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct)
        {
            _ = tenantId;
            _ = personReferenceId;
            _ = ct;
            return Task.FromResult(_result);
        }

        public Task<ReferenceValidationResult> ValidateOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId, CancellationToken ct)
        {
            _ = tenantId;
            _ = organizationUnitId;
            _ = ct;
            return Task.FromResult(_result);
        }

        public Task<ReferenceValidationResult> ValidatePositionAsync(Guid tenantId, Guid positionId, CancellationToken ct)
        {
            _ = tenantId;
            _ = positionId;
            _ = ct;
            return Task.FromResult(_result);
        }
    }

    private sealed class DataScopeEvaluator : ISensitiveAccessDataScopeEvaluator
    {
        private readonly SensitiveAccessDataScopeResult _result;

        private DataScopeEvaluator(SensitiveAccessDataScopeResult result) => _result = result;

        public static DataScopeEvaluator Allowed() => new(SensitiveAccessDataScopeResult.Allowed());
        public static DataScopeEvaluator Denied() => new(SensitiveAccessDataScopeResult.OutOfScope());

        public Task<SensitiveAccessDataScopeResult> EvaluateAsync(
            Guid tenantId,
            EmployeeProfileProjection projection,
            CancellationToken ct)
        {
            _ = tenantId;
            _ = projection;
            _ = ct;
            return Task.FromResult(_result);
        }
    }

    private sealed class InMemoryEmployeeProjectionRepository : IEmployeeProjectionRepository
    {
        private readonly Dictionary<Guid, EmployeeProfileProjection> _items;

        public InMemoryEmployeeProjectionRepository(params EmployeeProfileProjection[] items) =>
            _items = items.ToDictionary(item => item.Id);

        public Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult<IReadOnlyList<EmployeeProfileProjection>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());
        }

        public Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(
                _items.TryGetValue(id, out var item) && item.TenantId == tenantId && !item.IsDeleted
                    ? item
                    : null);
        }

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
        {
            _ = tenantId;
            _ = code;
            _ = excludingId;
            _ = ct;
            return Task.FromResult(false);
        }

        public Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPositionAssignmentRepository : IPositionAssignmentOverlayRepository
    {
        private readonly Dictionary<Guid, EmployeePositionAssignmentOverlay> _items;

        public InMemoryPositionAssignmentRepository(params EmployeePositionAssignmentOverlay[] items) =>
            _items = items.ToDictionary(item => item.Id);

        public Task<IReadOnlyList<EmployeePositionAssignmentOverlay>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult<IReadOnlyList<EmployeePositionAssignmentOverlay>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());
        }

        public Task<EmployeePositionAssignmentOverlay?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(
                _items.TryGetValue(id, out var item) && item.TenantId == tenantId && !item.IsDeleted
                    ? item
                    : null);
        }

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(_items.Values.Any(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));
        }

        public Task CreateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
        {
            _ = ct;
            _items[assignment.Id] = assignment;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
        {
            _ = ct;
            _items[assignment.Id] = assignment;
            return Task.CompletedTask;
        }
    }
}
