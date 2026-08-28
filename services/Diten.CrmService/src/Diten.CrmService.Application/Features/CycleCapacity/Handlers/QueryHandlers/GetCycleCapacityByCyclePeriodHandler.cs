using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>
/// "Does this period already have a capacity?" — the lookup behind the CyclePeriod row action. A 404 is a legitimate,
/// expected answer here (it means "not yet"), and the UI turns it into the create form rather than an error.
/// <para>Because the pin is 1:1 there is at most one answer by construction; the handler never has to choose.</para>
/// </summary>
public sealed class GetCycleCapacityByCyclePeriodHandler
    : IRequestHandler<GetCycleCapacityByCyclePeriodQuery, Response<CycleCapacityDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICycleCapacityRepository _capacities;
    private readonly ICyclePeriodReader _periods;
    private readonly ICycleCapacityCountryResolver _countries;

    public GetCycleCapacityByCyclePeriodHandler(
        ITenantContext tenant,
        ICycleCapacityRepository capacities,
        ICyclePeriodReader periods,
        ICycleCapacityCountryResolver countries)
    {
        _tenant = tenant;
        _capacities = capacities;
        _periods = periods;
        _countries = countries;
    }

    public async Task<Response<CycleCapacityDetailDto>> Handle(
        GetCycleCapacityByCyclePeriodQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CycleCapacityDetailDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _capacities.GetByCyclePeriodAsync(tenantId, request.CyclePeriodId, cancellationToken);
        if (entity is null)
        {
            return Response<CycleCapacityDetailDto>.Fail(
                "This cycle period does not have a capacity model yet.", 404);
        }

        var period = await _periods.GetByIdAsync(entity.CyclePeriodId, cancellationToken);
        var isDerived = period is not null
                        && _countries.Resolve(period, entity.CalendarCountryCode).IsDerived;

        return Response<CycleCapacityDetailDto>.Success(
            CycleCapacityMapper.ToDetail(entity, period, isDerived));
    }
}
