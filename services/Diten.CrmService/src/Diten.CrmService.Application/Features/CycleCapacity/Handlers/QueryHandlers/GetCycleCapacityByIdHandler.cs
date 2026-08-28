using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>One capacity. Another tenant's id answers 404 rather than 403, so the endpoint never confirms that a row
/// exists somewhere else.
/// <para>The pinned period is projected fresh on every read — never copied onto the capacity — so a renamed period
/// shows its new name immediately. The country resolver is consulted only to report whether the calendar country was
/// DERIVED from the period (the UI renders the field read-only in that case); it decides nothing here.</para></summary>
public sealed class GetCycleCapacityByIdHandler
    : IRequestHandler<GetCycleCapacityByIdQuery, Response<CycleCapacityDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICycleCapacityRepository _capacities;
    private readonly ICyclePeriodReader _periods;
    private readonly ICycleCapacityCountryResolver _countries;

    public GetCycleCapacityByIdHandler(
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
        GetCycleCapacityByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CycleCapacityDetailDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _capacities.GetByIdAsync(tenantId, request.CycleCapacityId, cancellationToken);
        if (entity is null)
        {
            return Response<CycleCapacityDetailDto>.Fail("Cycle capacity not found.", 404);
        }

        var period = await _periods.GetByIdAsync(entity.CyclePeriodId, cancellationToken);
        var isDerived = period is not null
                        && _countries.Resolve(period, entity.CalendarCountryCode).IsDerived;

        return Response<CycleCapacityDetailDto>.Success(
            CycleCapacityMapper.ToDetail(entity, period, isDerived));
    }
}
