using System.Text.RegularExpressions;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.HcmService.Persistence.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private const string EmployeeCollectionName = "hcm_employees";
    private const string EmploymentRecordCollectionName = "hcm_employment_records";

    private readonly IMongoCollection<Employee> _employees;
    private readonly IMongoCollection<EmploymentRecord> _employmentRecords;

    public EmployeeRepository(IMongoDatabase database)
    {
        _employees = database.GetCollection<Employee>(EmployeeCollectionName);
        _employmentRecords = database.GetCollection<EmploymentRecord>(EmploymentRecordCollectionName);
    }

    public async Task<bool> EmployeeNumberExistsAsync(
        Guid tenantId,
        string employeeNumber,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Employee>.Filter.And(
            Builders<Employee>.Filter.Eq(employee => employee.TenantId, tenantId),
            Builders<Employee>.Filter.Eq(employee => employee.EmployeeNumber, employeeNumber),
            Builders<Employee>.Filter.Eq(employee => employee.IsDeleted, false));

        return await _employees.CountDocumentsAsync(filter, cancellationToken: cancellationToken) > 0;
    }

    public async Task<EmployeeRegistrySearchResult> SearchRegistryAsync(
        Guid tenantId,
        EmployeeRegistrySearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var employeeFilter = await BuildEmployeeFilterAsync(tenantId, criteria, cancellationToken);
        if (employeeFilter is null)
        {
            return new EmployeeRegistrySearchResult([], 0);
        }

        var total = await _employees.CountDocumentsAsync(employeeFilter, cancellationToken: cancellationToken);
        var employees = await _employees
            .Find(employeeFilter)
            .Sort(BuildSort(criteria))
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Limit(criteria.PageSize)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return new EmployeeRegistrySearchResult([], total);
        }

        var employeeIds = employees.Select(employee => employee.Id).ToArray();
        var employmentFilter = Builders<EmploymentRecord>.Filter.And(
            Builders<EmploymentRecord>.Filter.Eq(record => record.TenantId, tenantId),
            Builders<EmploymentRecord>.Filter.Eq(record => record.IsDeleted, false),
            Builders<EmploymentRecord>.Filter.In(record => record.EmployeeId, employeeIds));

        var employmentRecords = await _employmentRecords
            .Find(employmentFilter)
            .ToListAsync(cancellationToken);

        var primaryEmploymentByEmployee = employmentRecords
            .GroupBy(record => record.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(record => record.StartDate)
                    .ThenByDescending(record => record.Id)
                    .First());

        var rows = employees
            .Select(employee => new EmployeeRegistryEntry(
                employee,
                primaryEmploymentByEmployee.TryGetValue(employee.Id, out var employmentRecord) ? employmentRecord : null))
            .ToArray();

        return new EmployeeRegistrySearchResult(rows, total);
    }

    public async Task<EmployeeDetailRecord?> GetDetailAsync(
        Guid tenantId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employeeFilter = Builders<Employee>.Filter.And(
            Builders<Employee>.Filter.Eq(employee => employee.TenantId, tenantId),
            Builders<Employee>.Filter.Eq(employee => employee.Id, employeeId),
            Builders<Employee>.Filter.Eq(employee => employee.IsDeleted, false));

        var employee = await _employees
            .Find(employeeFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return null;
        }

        var employmentFilter = Builders<EmploymentRecord>.Filter.And(
            Builders<EmploymentRecord>.Filter.Eq(record => record.TenantId, tenantId),
            Builders<EmploymentRecord>.Filter.Eq(record => record.EmployeeId, employeeId),
            Builders<EmploymentRecord>.Filter.Eq(record => record.IsDeleted, false));

        var employmentRecords = await _employmentRecords
            .Find(employmentFilter)
            .SortByDescending(record => record.StartDate)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken);

        return new EmployeeDetailRecord(employee, employmentRecords);
    }

    private async Task<FilterDefinition<Employee>?> BuildEmployeeFilterAsync(
        Guid tenantId,
        EmployeeRegistrySearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Employee>.Filter;
        var filters = new List<FilterDefinition<Employee>>
        {
            builder.Eq(employee => employee.TenantId, tenantId),
            builder.Eq(employee => employee.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(criteria.Search), "i");
            filters.Add(builder.Or(
                builder.Regex(employee => employee.EmployeeNumber, regex),
                builder.Regex(employee => employee.LegalFirstName, regex),
                builder.Regex(employee => employee.LegalMiddleName, regex),
                builder.Regex(employee => employee.LegalLastName, regex),
                builder.Regex(employee => employee.PreferredName, regex)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.EmployeeStatus))
        {
            filters.Add(builder.Eq(employee => employee.EmployeeStatus, criteria.EmployeeStatus));
        }

        if (!string.IsNullOrWhiteSpace(criteria.WorkerType))
        {
            filters.Add(builder.Eq(employee => employee.WorkerType, criteria.WorkerType));
        }

        if (!string.IsNullOrWhiteSpace(criteria.EmploymentType))
        {
            filters.Add(builder.Eq(employee => employee.EmploymentType, criteria.EmploymentType));
        }

        if (criteria.LegalEntityId is { } legalEntityId)
        {
            var employmentFilter = Builders<EmploymentRecord>.Filter.And(
                Builders<EmploymentRecord>.Filter.Eq(record => record.TenantId, tenantId),
                Builders<EmploymentRecord>.Filter.Eq(record => record.IsDeleted, false),
                Builders<EmploymentRecord>.Filter.Eq(record => record.LegalEntityId, legalEntityId));

            var employeeIds = await _employmentRecords
                .Find(employmentFilter)
                .Project(record => record.EmployeeId)
                .ToListAsync(cancellationToken);

            if (employeeIds.Count == 0)
            {
                return null;
            }

            filters.Add(builder.In(employee => employee.Id, employeeIds));
        }

        return builder.And(filters);
    }

    private static SortDefinition<Employee> BuildSort(EmployeeRegistrySearchCriteria criteria)
    {
        var sortBuilder = Builders<Employee>.Sort;
        return criteria.SortBy.ToLowerInvariant() switch
        {
            "employeenumber" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.EmployeeNumber)
                : sortBuilder.Ascending(employee => employee.EmployeeNumber),
            "displayname" => criteria.SortDescending
                ? sortBuilder.Combine(
                    sortBuilder.Descending(employee => employee.LegalLastName),
                    sortBuilder.Descending(employee => employee.LegalFirstName))
                : sortBuilder.Combine(
                    sortBuilder.Ascending(employee => employee.LegalLastName),
                    sortBuilder.Ascending(employee => employee.LegalFirstName)),
            "workertype" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.WorkerType)
                : sortBuilder.Ascending(employee => employee.WorkerType),
            "employmenttype" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.EmploymentType)
                : sortBuilder.Ascending(employee => employee.EmploymentType),
            "employeestatus" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.EmployeeStatus)
                : sortBuilder.Ascending(employee => employee.EmployeeStatus),
            "sensitivitylevel" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.SensitivityLevel)
                : sortBuilder.Ascending(employee => employee.SensitivityLevel),
            "hiredate" => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.HireDate)
                : sortBuilder.Ascending(employee => employee.HireDate),
            _ => criteria.SortDescending
                ? sortBuilder.Descending(employee => employee.UpdatedAt)
                : sortBuilder.Ascending(employee => employee.UpdatedAt)
        };
    }

}
