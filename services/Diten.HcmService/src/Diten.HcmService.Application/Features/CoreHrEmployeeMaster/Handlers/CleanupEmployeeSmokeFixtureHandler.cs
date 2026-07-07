using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class CleanupEmployeeSmokeFixtureHandler
    : IRequestHandler<CleanupEmployeeSmokeFixtureCommand, Response<EmployeeSmokeFixtureCleanupResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeSmokeFixtureRepository _repository;

    public CleanupEmployeeSmokeFixtureHandler(
        ITenantContext tenantContext,
        IEmployeeSmokeFixtureRepository repository)
    {
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<EmployeeSmokeFixtureCleanupResponse>> Handle(
        CleanupEmployeeSmokeFixtureCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.IsLocalFixtureEnabled)
        {
            return Response<EmployeeSmokeFixtureCleanupResponse>.Fail("HCM smoke fixtures are not available in this environment.", 404);
        }

        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeSmokeFixtureCleanupResponse>();
        }

        var result = await _repository.CleanupMinimalEmployeeAsync(tenantId, cancellationToken);
        return Response<EmployeeSmokeFixtureCleanupResponse>.Success(new EmployeeSmokeFixtureCleanupResponse(
            result.EmployeeId,
            tenantId,
            result.EmployeeNumber,
            result.Deleted,
            result.WasPresent));
    }
}
