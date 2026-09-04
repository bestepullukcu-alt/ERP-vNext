using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;

/// <summary>Loads one plan's detail. A cross-tenant id resolves to nothing and returns 404 (no authorization leak).</summary>
public sealed class GetPlannedVisitByIdHandler
    : IRequestHandler<GetPlannedVisitByIdQuery, Response<PlannedVisitDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _repository;

    public GetPlannedVisitByIdHandler(ITenantContext tenant, IPlannedVisitRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<PlannedVisitDetailDto>> Handle(
        GetPlannedVisitByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlannedVisitDetailDto>.Fail("Tenant context is required.", 400);
        }

        var plan = await _repository.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        return plan is null
            ? Response<PlannedVisitDetailDto>.Fail("Planned visit not found.", 404)
            : Response<PlannedVisitDetailDto>.Success(PlannedVisitMapper.ToDetail(plan));
    }
}
