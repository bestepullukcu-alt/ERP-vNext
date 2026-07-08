using Diten.HcmService.Domain.Entities;

namespace Diten.HcmService.Domain.Repositories;

public interface IEmployeeRepository
{
    Task<bool> EmployeeNumberExistsAsync(
        Guid tenantId,
        string employeeNumber,
        CancellationToken cancellationToken);

    Task<EmployeeRegistrySearchResult> SearchRegistryAsync(
        Guid tenantId,
        EmployeeRegistrySearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<EmployeeDetailRecord?> GetDetailAsync(
        Guid tenantId,
        Guid employeeId,
        CancellationToken cancellationToken);
}

public sealed record EmployeeRegistrySearchCriteria(
    string? Search,
    string? EmployeeStatus,
    string? WorkerType,
    string? EmploymentType,
    Guid? LegalEntityId,
    int Page,
    int PageSize,
    string SortBy,
    bool SortDescending);

public sealed record EmployeeRegistrySearchResult(
    IReadOnlyList<EmployeeRegistryEntry> Items,
    long TotalCount);

public sealed record EmployeeRegistryEntry(
    Employee Employee,
    EmploymentRecord? PrimaryEmploymentRecord);

public sealed record EmployeeDetailRecord(
    Employee Employee,
    IReadOnlyList<EmploymentRecord> EmploymentRecords);
