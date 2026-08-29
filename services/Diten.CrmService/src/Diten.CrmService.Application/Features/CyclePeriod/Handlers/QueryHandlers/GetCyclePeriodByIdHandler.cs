using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>One period. Another tenant's id answers 404 rather than 403, so the endpoint never confirms that a row
/// exists somewhere else.</summary>
public sealed class GetCyclePeriodByIdHandler
    : IRequestHandler<GetCyclePeriodByIdQuery, Response<CyclePeriodDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;

    public GetCyclePeriodByIdHandler(ITenantContext tenant, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _periods = periods;
    }

    public async Task<Response<CyclePeriodDetailDto>> Handle(
        GetCyclePeriodByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CyclePeriodDetailDto>.Fail("Tenant context is required.", 400);
        }

        var period = await _periods.GetByIdAsync(tenantId, request.CyclePeriodId, cancellationToken);
        return period is null
            ? Response<CyclePeriodDetailDto>.Fail("Cycle period not found.", 404)
            : Response<CyclePeriodDetailDto>.Success(CyclePeriodMapper.ToDetail(period));
    }
}
