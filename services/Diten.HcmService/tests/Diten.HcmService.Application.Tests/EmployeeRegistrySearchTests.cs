using System.Reflection;
using System.Text.Json;
using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using Diten.HcmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeRegistrySearchTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task SearchEmployees_UsesTenantContext_AndReturnsSafeRegistryProjection()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        var employee = Employee(TenantA, "E-001", "Ada", "Lovelace");
        employee.PersonalEmail = "private@example.test";
        employee.Phone = "+1-555-0000";
        employee.DateOfBirth = new DateOnly(1815, 12, 10);
        repository.Seed(employee, Employment(employee.TenantId, employee.Id));
        repository.Seed(Employee(TenantB, "E-999", "Cross", "Tenant"), null);
        repository.Seed(Employee(TenantA, "E-002", "Deleted", "Person", isDeleted: true), null);
        var handler = new SearchEmployeesHandler(tenantContext, repository);

        var response = await handler.Handle(Query(actionPermissions: new EmployeeRegistryActionPermissions(true, false, false, false, false, false)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TenantA, repository.LastTenantId);
        var row = Assert.Single(response.Data!.Items);
        Assert.Equal(employee.Id, row.EmployeeId);
        Assert.Equal(employee.PersonId, row.PersonId);
        Assert.Equal("Ada Lovelace", row.DisplayName);
        var json = JsonSerializer.Serialize(row);
        Assert.DoesNotContain("TenantId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PersonalEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private@example.test", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchEmployees_ClampsPaging_AndFallsBackInvalidSort()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        var employee = Employee(TenantA, "E-001", "Ada", "Lovelace");
        repository.Seed(employee, null);
        var handler = new SearchEmployeesHandler(tenantContext, repository);

        var response = await handler.Handle(
            Query(page: -10, pageSize: 500, sortBy: "tenantId", sortDirection: "desc"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, repository.LastCriteria!.Page);
        Assert.Equal(100, repository.LastCriteria.PageSize);
        Assert.Equal("updatedAt", repository.LastCriteria.SortBy);
        Assert.True(repository.LastCriteria.SortDescending);
    }

    [Fact]
    public async Task SearchEmployees_RowActionsReflectBoundedPermissionSnapshot()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        repository.Seed(Employee(TenantA, "E-001", "Ada", "Lovelace"), null);
        var handler = new SearchEmployeesHandler(tenantContext, repository);

        var response = await handler.Handle(
            Query(actionPermissions: new EmployeeRegistryActionPermissions(
                CanView: true,
                CanEditLegal: false,
                CanEditEmployment: false,
                CanChangeStatus: false,
                CanAttachEvidence: false,
                CanExport: false)),
            CancellationToken.None);

        var actions = Assert.Single(response.Data!.Items).Actions;
        Assert.True(actions.CanView);
        Assert.False(actions.CanEditLegal);
        Assert.False(actions.CanEditEmployment);
        Assert.False(actions.CanChangeStatus);
        Assert.False(actions.CanAttachEvidence);
        Assert.False(actions.CanExport);
    }

    [Fact]
    public async Task SearchEmployees_MissingTenant_ReturnsBadRequest()
    {
        var handler = new SearchEmployeesHandler(new TenantContext(), new InMemoryEmployeeRepository());

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void EmployeesController_SearchEndpoint_RequiresEmployeeSearchPermission()
    {
        var method = typeof(Diten.HcmService.Api.Controllers.Hcm.EmployeesController)
            .GetMethod("Search", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var permission = Assert.Single(method!.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal("mod0251.employee.search", permission.Permission);
    }

    [Fact]
    public async Task GetEmployee_UsesTenantContext_AndReturnsSafeDetailProjection()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        var employee = Employee(TenantA, "E-010", "Grace", "Hopper");
        employee.LegalMiddleName = "Brewster";
        employee.PreferredName = "Amazing Grace";
        employee.NationalityCode = "US";
        employee.WorkEmail = "grace.hopper@example.test";
        employee.PersonalEmail = "private@example.test";
        employee.Phone = "+1-555-0000";
        employee.DateOfBirth = new DateOnly(1906, 12, 9);
        repository.Seed(employee, Employment(employee.TenantId, employee.Id));
        repository.Seed(Employee(TenantB, "E-999", "Cross", "Tenant"), null);
        var handler = new GetEmployeeHandler(tenantContext, repository);

        var response = await handler.Handle(new GetEmployeeQuery(employee.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TenantA, repository.LastTenantId);
        Assert.Equal(employee.Id, response.Data!.EmployeeId);
        Assert.Equal(employee.EmployeeNumber, response.Data.EmployeeNumber);
        Assert.Equal(employee.PersonId, response.Data.PersonId);
        Assert.Equal("Grace", response.Data.LegalProfile.LegalFirstName);
        Assert.Equal("Brewster", response.Data.LegalProfile.LegalMiddleName);
        Assert.Equal("Hopper", response.Data.LegalProfile.LegalLastName);
        Assert.Equal("Amazing Grace", response.Data.LegalProfile.PreferredName);
        Assert.Equal("US", response.Data.LegalProfile.NationalityCode);
        Assert.Equal("grace.hopper@example.test", response.Data.LegalProfile.WorkEmail);
        Assert.Null(response.Data.LegalProfile.DateOfBirth);
        Assert.Null(response.Data.LegalProfile.PersonalEmail);
        Assert.Null(response.Data.LegalProfile.Phone);
        Assert.False(response.Data.LegalProfile.GovernmentIdentifierPresent);
        Assert.True(response.Data.SensitiveFieldsMasked);
        Assert.Single(response.Data.EmploymentRecords);

        var json = JsonSerializer.Serialize(response.Data);
        Assert.DoesNotContain("TenantId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private@example.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+1-555-0000", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1906", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GovernmentIdentifierValue", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEmployee_CrossTenant_ReturnsNotFound()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        var employee = Employee(TenantB, "E-999", "Cross", "Tenant");
        repository.Seed(employee, Employment(employee.TenantId, employee.Id));
        var handler = new GetEmployeeHandler(tenantContext, repository);

        var response = await handler.Handle(new GetEmployeeQuery(employee.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployee_SoftDeleted_ReturnsNotFound()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantA);
        var repository = new InMemoryEmployeeRepository();
        var employee = Employee(TenantA, "E-011", "Deleted", "Person", isDeleted: true);
        repository.Seed(employee, Employment(employee.TenantId, employee.Id));
        var handler = new GetEmployeeHandler(tenantContext, repository);

        var response = await handler.Handle(new GetEmployeeQuery(employee.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public void EmployeesController_GetEndpoint_RequiresEmployeeViewPermission()
    {
        var method = typeof(Diten.HcmService.Api.Controllers.Hcm.EmployeesController)
            .GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var permission = Assert.Single(method!.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal("mod0251.employee.view", permission.Permission);
    }

    private static SearchEmployeesQuery Query(
        int page = 1,
        int pageSize = 20,
        string? sortBy = "updatedAt",
        string? sortDirection = "desc",
        EmployeeRegistryActionPermissions? actionPermissions = null)
        => new(
            Search: null,
            EmployeeStatus: null,
            WorkerType: null,
            EmploymentType: null,
            LegalEntityId: null,
            Page: page,
            PageSize: pageSize,
            SortBy: sortBy,
            SortDirection: sortDirection,
            ActionPermissions: actionPermissions ?? new EmployeeRegistryActionPermissions(false, false, false, false, false, false));

    private static Employee Employee(Guid tenantId, string employeeNumber, string firstName, string lastName, bool isDeleted = false)
        => new()
        {
            TenantId = tenantId,
            PersonId = Guid.NewGuid(),
            EmployeeNumber = employeeNumber,
            LegalFirstName = firstName,
            LegalLastName = lastName,
            WorkerType = "employee",
            EmploymentType = "full_time",
            EmployeeStatus = "active",
            SensitivityLevel = "standard",
            HireDate = new DateOnly(2026, 1, 1),
            IsDeleted = isDeleted
        };

    private static EmploymentRecord Employment(Guid tenantId, Guid employeeId)
        => new()
        {
            TenantId = tenantId,
            EmployeeId = employeeId,
            LegalEntityId = Guid.Parse("02510000-0000-0000-0000-000000000004"),
            OrganizationUnitId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 1, 1),
            EmploymentStatus = "active"
        };

    private sealed class InMemoryEmployeeRepository : IEmployeeRepository
    {
        private readonly List<EmployeeRegistryEntry> _entries = [];

        public Guid LastTenantId { get; private set; }
        public EmployeeRegistrySearchCriteria? LastCriteria { get; private set; }

        public void Seed(Employee employee, EmploymentRecord? employmentRecord)
        {
            _entries.Add(new EmployeeRegistryEntry(employee, employmentRecord));
        }

        public Task<bool> EmployeeNumberExistsAsync(Guid tenantId, string employeeNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries.Any(entry =>
                entry.Employee.TenantId == tenantId &&
                string.Equals(entry.Employee.EmployeeNumber, employeeNumber, StringComparison.Ordinal) &&
                !entry.Employee.IsDeleted));
        }

        public Task<EmployeeRegistrySearchResult> SearchRegistryAsync(
            Guid tenantId,
            EmployeeRegistrySearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            LastCriteria = criteria;

            var filtered = _entries
                .Where(entry => entry.Employee.TenantId == tenantId && !entry.Employee.IsDeleted)
                .Where(entry => criteria.LegalEntityId is null || entry.PrimaryEmploymentRecord?.LegalEntityId == criteria.LegalEntityId)
                .ToArray();

            var page = filtered
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToArray();

            return Task.FromResult(new EmployeeRegistrySearchResult(page, filtered.Length));
        }

        public Task<EmployeeDetailRecord?> GetDetailAsync(
            Guid tenantId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            var employee = _entries
                .Select(entry => entry.Employee)
                .FirstOrDefault(candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.Id == employeeId &&
                    !candidate.IsDeleted);

            if (employee is null)
            {
                return Task.FromResult<EmployeeDetailRecord?>(null);
            }

            var records = _entries
                .Select(entry => entry.PrimaryEmploymentRecord)
                .Where(record =>
                    record is not null &&
                    record.TenantId == tenantId &&
                    record.EmployeeId == employeeId &&
                    !record.IsDeleted)
                .Select(record => record!)
                .ToArray();

            return Task.FromResult<EmployeeDetailRecord?>(new EmployeeDetailRecord(employee, records));
        }
    }
}
