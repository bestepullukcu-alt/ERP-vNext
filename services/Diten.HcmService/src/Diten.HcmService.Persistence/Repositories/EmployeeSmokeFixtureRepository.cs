using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HcmService.Persistence.Repositories;

public sealed class EmployeeSmokeFixtureRepository : IEmployeeSmokeFixtureRepository
{
    private static readonly Guid FixturePersonId = Guid.Parse("02510000-0000-0000-0000-000000000001");
    private static readonly Guid FixtureActorId = Guid.Parse("02510000-0000-0000-0000-000000000007");
    private const string EmployeeCollectionName = "hcm_employees";

    private readonly IMongoCollection<Employee> _employees;

    public EmployeeSmokeFixtureRepository(IMongoDatabase database)
    {
        _employees = database.GetCollection<Employee>(EmployeeCollectionName);
    }

    public async Task<EmployeeSmokeFixtureEnsureResult> EnsureMinimalEmployeeAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Employee>.Filter.And(
            Builders<Employee>.Filter.Eq(employee => employee.TenantId, tenantId),
            Builders<Employee>.Filter.Eq(employee => employee.EmployeeNumber, EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber));

        var existing = await _employees
            .Find(filter)
            .SortByDescending(employee => employee.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var now = DateTimeOffset.UtcNow;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.PersonId = FixturePersonId;
            existing.LegalFirstName = "Smoke";
            existing.LegalMiddleName = null;
            existing.LegalLastName = "Employee";
            existing.PreferredName = "Smoke Employee";
            existing.DateOfBirth = null;
            existing.NationalityCode = null;
            existing.WorkEmail = null;
            existing.PersonalEmail = null;
            existing.Phone = null;
            existing.EmployeeStatus = "draft";
            existing.WorkerType = "employee";
            existing.EmploymentType = "full_time";
            existing.HireDate = null;
            existing.TerminationDate = null;
            existing.SensitivityLevel = "standard";
            existing.UpdatedBy = FixtureActorId;
            existing.UpdatedAt = now;
            existing.Version += 1;
            existing.ETag = Employee.CreateETag(existing.Version);

            await _employees.ReplaceOneAsync(
                employee => employee.Id == existing.Id && employee.TenantId == tenantId,
                existing,
                cancellationToken: cancellationToken);

            return new EmployeeSmokeFixtureEnsureResult(existing, Created: false, Reused: true);
        }

        var createdAt = DateTimeOffset.UtcNow;
        var employee = new Employee
        {
            TenantId = tenantId,
            PersonId = FixturePersonId,
            EmployeeNumber = EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber,
            LegalFirstName = "Smoke",
            LegalLastName = "Employee",
            PreferredName = "Smoke Employee",
            EmployeeStatus = "draft",
            WorkerType = "employee",
            EmploymentType = "full_time",
            SensitivityLevel = "standard",
            CreatedBy = FixtureActorId,
            UpdatedBy = FixtureActorId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Version = 1,
            ETag = Employee.CreateETag(1)
        };

        await _employees.InsertOneAsync(employee, cancellationToken: cancellationToken);
        return new EmployeeSmokeFixtureEnsureResult(employee, Created: true, Reused: false);
    }

    public async Task<EmployeeSmokeFixtureCleanupResult> CleanupMinimalEmployeeAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Employee>.Filter.And(
            Builders<Employee>.Filter.Eq(employee => employee.TenantId, tenantId),
            Builders<Employee>.Filter.Eq(employee => employee.EmployeeNumber, EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber),
            Builders<Employee>.Filter.Eq(employee => employee.IsDeleted, false));

        var employee = await _employees.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return new EmployeeSmokeFixtureCleanupResult(
                null,
                EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber,
                Deleted: false,
                WasPresent: false);
        }

        var now = DateTimeOffset.UtcNow;
        employee.IsDeleted = true;
        employee.DeletedAt = now;
        employee.UpdatedAt = now;
        employee.UpdatedBy = FixtureActorId;
        employee.Version += 1;
        employee.ETag = Employee.CreateETag(employee.Version);

        await _employees.ReplaceOneAsync(
            current => current.Id == employee.Id && current.TenantId == tenantId && !current.IsDeleted,
            employee,
            cancellationToken: cancellationToken);

        return new EmployeeSmokeFixtureCleanupResult(
            employee.Id,
            employee.EmployeeNumber,
            Deleted: true,
            WasPresent: true);
    }
}
