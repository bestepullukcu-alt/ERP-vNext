using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Domain.Repositories;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class GetEmployeeDraftHandler : IRequestHandler<GetEmployeeDraftQuery, Response<EmployeeDraftResponse>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IEmployeeDraftSessionRepository _repository;

    public GetEmployeeDraftHandler(ITenantContext tenantContext, IEmployeeDraftSessionRepository repository)
    {
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<EmployeeDraftResponse>> Handle(GetEmployeeDraftQuery request, CancellationToken cancellationToken)
    {
        if (!EmployeeDraftHandlerHelpers.TryGetTenantId(_tenantContext, out var tenantId))
        {
            return EmployeeDraftHandlerHelpers.MissingTenant<EmployeeDraftResponse>();
        }

        var draftSession = await _repository.GetByIdAsync(tenantId, request.DraftSessionId, cancellationToken);
        return draftSession is null
            ? Response<EmployeeDraftResponse>.Fail("Draft session not found.", 404)
            : Response<EmployeeDraftResponse>.Success(EmployeeDraftMapper.ToDraftResponse(draftSession));
    }
}
