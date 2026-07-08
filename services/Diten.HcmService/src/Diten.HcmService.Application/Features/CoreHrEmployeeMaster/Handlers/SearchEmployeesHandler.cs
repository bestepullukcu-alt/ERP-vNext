using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class SearchEmployeesHandler : IRequestHandler<SearchEmployeesQuery, Response<EmployeeRegistrySearchResponse>>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "employeeNumber",
        "displayName",
        "workerType",
        "employmentType",
        "employeeStatus",
        "sensitivityLevel",
        "hireDate",
        "updatedAt"
    };

    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeRepository _repository;

    public SearchEmployeesHandler(ITenantContext tenantContext, IEmployeeRepository repository)
    {
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<EmployeeRegistrySearchResponse>> Handle(SearchEmployeesQuery request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeRegistrySearchResponse>();
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? DefaultPageSize : request.PageSize, 1, MaxPageSize);
        var sortBy = AllowedSortFields.Contains(request.SortBy ?? string.Empty)
            ? request.SortBy!
            : "updatedAt";
        var sortDescending = !string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var result = await _repository.SearchRegistryAsync(
            tenantId,
            new EmployeeRegistrySearchCriteria(
                Normalize(request.Search),
                Normalize(request.EmployeeStatus),
                Normalize(request.WorkerType),
                Normalize(request.EmploymentType),
                request.LegalEntityId,
                page,
                pageSize,
                sortBy,
                sortDescending),
            cancellationToken);

        var rows = result.Items
            .Select(entry => ToRow(entry.Employee, entry.PrimaryEmploymentRecord, request.ActionPermissions))
            .ToArray();

        return Response<EmployeeRegistrySearchResponse>.Success(new EmployeeRegistrySearchResponse(
            rows,
            page,
            pageSize,
            result.TotalCount));
    }

    private static EmployeeRegistryRowResponse ToRow(
        Employee employee,
        EmploymentRecord? employmentRecord,
        EmployeeRegistryActionPermissions actions)
    {
        var legalEntityId = employmentRecord?.LegalEntityId ?? Guid.Empty;
        return new EmployeeRegistryRowResponse(
            employee.Id,
            employee.EmployeeNumber,
            employee.PersonId,
            BuildDisplayName(employee),
            employee.WorkerType,
            employee.EmploymentType,
            legalEntityId,
            legalEntityId == Guid.Empty ? string.Empty : legalEntityId.ToString("D"),
            employee.EmployeeStatus,
            employee.SensitivityLevel,
            employee.HireDate,
            employee.UpdatedAt,
            employee.Version,
            employee.ETag,
            new EmployeeRowActions(
                actions.CanView,
                actions.CanEditLegal,
                actions.CanEditEmployment,
                actions.CanChangeStatus,
                actions.CanAttachEvidence,
                actions.CanExport));
    }

    private static string BuildDisplayName(Employee employee)
    {
        if (!string.IsNullOrWhiteSpace(employee.PreferredName))
        {
            return employee.PreferredName.Trim();
        }

        return string.Join(
            ' ',
            new[] { employee.LegalFirstName, employee.LegalMiddleName, employee.LegalLastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
