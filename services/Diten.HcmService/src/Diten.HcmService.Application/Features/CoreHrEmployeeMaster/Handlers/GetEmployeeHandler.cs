using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class GetEmployeeHandler : IRequestHandler<GetEmployeeQuery, Response<EmployeeDetailResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeRepository _repository;

    public GetEmployeeHandler(ITenantContext tenantContext, IEmployeeRepository repository)
    {
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<EmployeeDetailResponse>> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeDetailResponse>();
        }

        var detail = await _repository.GetDetailAsync(tenantId, request.EmployeeId, cancellationToken);
        if (detail is null)
        {
            return Response<EmployeeDetailResponse>.Fail("Employee not found.", 404);
        }

        return Response<EmployeeDetailResponse>.Success(ToResponse(detail.Employee, detail.EmploymentRecords));
    }

    private static EmployeeDetailResponse ToResponse(Employee employee, IReadOnlyList<EmploymentRecord> employmentRecords)
        => new(
            employee.Id,
            employee.EmployeeNumber,
            employee.PersonId,
            new EmployeeLegalProfileResponse(
                employee.LegalFirstName,
                employee.LegalMiddleName,
                employee.LegalLastName,
                employee.PreferredName,
                DateOfBirth: null,
                employee.NationalityCode,
                employee.WorkEmail,
                PersonalEmail: null,
                Phone: null,
                GovernmentIdentifierPresent: false),
            employmentRecords
                .OrderByDescending(record => record.StartDate)
                .ThenByDescending(record => record.Id)
                .Select(ToEmploymentRecord)
                .ToArray(),
            employee.EmployeeStatus,
            employee.SensitivityLevel,
            SensitiveFieldsMasked: true,
            employee.Version,
            employee.ETag,
            employee.UpdatedAt);

    private static EmploymentRecordResponse ToEmploymentRecord(EmploymentRecord record)
        => new(
            record.Id,
            record.LegalEntityId,
            record.OrganizationUnitId,
            record.PositionId,
            record.StartDate,
            record.EndDate,
            record.ContractType,
            record.ProbationStatus,
            record.ProbationEndDate,
            record.EmploymentStatus,
            record.ApprovalStatus,
            record.Version,
            record.ETag);
}
