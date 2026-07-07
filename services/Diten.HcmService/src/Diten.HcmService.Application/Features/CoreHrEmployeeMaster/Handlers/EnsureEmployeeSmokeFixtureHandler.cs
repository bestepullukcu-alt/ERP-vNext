using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class EnsureEmployeeSmokeFixtureHandler
    : IRequestHandler<EnsureEmployeeSmokeFixtureCommand, Response<EmployeeSmokeFixtureResponse>>
{
    public const string FixtureEmployeeNumber = "MOD0251-SMOKE-DETAIL-EMPLOYEE";

    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeSmokeFixtureRepository _repository;

    public EnsureEmployeeSmokeFixtureHandler(
        ITenantContext tenantContext,
        IEmployeeSmokeFixtureRepository repository)
    {
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<EmployeeSmokeFixtureResponse>> Handle(
        EnsureEmployeeSmokeFixtureCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.IsLocalFixtureEnabled)
        {
            return Response<EmployeeSmokeFixtureResponse>.Fail("HCM smoke fixtures are not available in this environment.", 404);
        }

        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeSmokeFixtureResponse>();
        }

        var result = await _repository.EnsureMinimalEmployeeAsync(tenantId, cancellationToken);
        return Response<EmployeeSmokeFixtureResponse>.Success(new EmployeeSmokeFixtureResponse(
            result.Employee.Id,
            result.Employee.TenantId,
            result.Employee.EmployeeNumber,
            result.Employee.EmployeeStatus,
            result.Created,
            result.Reused,
            $"/api/v1/hcm/employees/{result.Employee.Id:D}",
            result.Employee.UpdatedAt), result.Created ? 201 : 200);
    }
}
