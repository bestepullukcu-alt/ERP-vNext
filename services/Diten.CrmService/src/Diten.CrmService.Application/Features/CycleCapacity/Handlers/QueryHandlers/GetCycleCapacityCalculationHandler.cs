using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;

/// <summary>
/// The estimate for a SAVED capacity.
/// <para>The month resolution and the fail-closed policy live in <see cref="CycleCapacityEstimator"/>, shared with the
/// live preview: one rule, so a number the form shows while typing is the same number the saved record reports. This
/// handler only loads the row and its period, and shapes the HTTP answer.</para>
/// <para><b>Nothing is written.</b> Not the figure, not a cache, not a "last calculated" stamp. Working calendars
/// change, and a stored number would start lying the moment they do (D-PROJECTION).</para>
/// </summary>
public sealed class GetCycleCapacityCalculationHandler
    : IRequestHandler<GetCycleCapacityCalculationQuery, Response<CycleCapacityCalculationDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICycleCapacityRepository _capacities;
    private readonly ICyclePeriodReader _periods;
    private readonly CycleCapacityEstimator _estimator;

    public GetCycleCapacityCalculationHandler(
        ITenantContext tenant,
        ICycleCapacityRepository capacities,
        ICyclePeriodReader periods,
        CycleCapacityEstimator estimator)
    {
        _tenant = tenant;
        _capacities = capacities;
        _periods = periods;
        _estimator = estimator;
    }

    public async Task<Response<CycleCapacityCalculationDto>> Handle(
        GetCycleCapacityCalculationQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CycleCapacityCalculationDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _capacities.GetByIdAsync(tenantId, request.CycleCapacityId, cancellationToken);
        if (entity is null)
        {
            return Response<CycleCapacityCalculationDto>.Fail("Cycle capacity not found.", 404);
        }

        // A period that can no longer be read is answered as an unresolved CALCULATION rather than a 404 on the
        // capacity, which does exist.
        var period = await _periods.GetByIdAsync(entity.CyclePeriodId, cancellationToken);
        var estimate = await _estimator.EstimateAsync(entity, period, cancellationToken);

        return CycleCapacityCalculationResponse.From(entity, estimate);
    }
}
