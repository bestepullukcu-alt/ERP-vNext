using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;

public sealed record SearchEmployeesQuery(
    string? Search,
    string? EmployeeStatus,
    string? WorkerType,
    string? EmploymentType,
    Guid? LegalEntityId,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDirection,
    EmployeeRegistryActionPermissions ActionPermissions) : IRequest<Response<EmployeeRegistrySearchResponse>>;

public sealed record EmployeeRegistryActionPermissions(
    bool CanView,
    bool CanEditLegal,
    bool CanEditEmployment,
    bool CanChangeStatus,
    bool CanAttachEvidence,
    bool CanExport);
