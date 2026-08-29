using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>
/// The capacity pinned to one period, or 404. This is what the CyclePeriod row action resolves against: the UI asks
/// "does this period already have a capacity?" and routes to the detail page or the create form accordingly.
/// <para>It is the 1:1 lookup, so it answers with at most one record by construction.</para>
/// </summary>
public sealed record GetCycleCapacityByCyclePeriodQuery(Guid CyclePeriodId)
    : IRequest<Response<CycleCapacityDetailDto>>;
