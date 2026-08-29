using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>
/// The estimate. A READ that reaches the platform working calendar over HTTP, once per month of the pinned period.
/// <para>It writes nothing — not the figure, not a cache, not an audit row — and it is fail-closed: if any month fails
/// to resolve, the whole answer comes back unresolved with a null total and an empty month list.</para>
/// </summary>
public sealed record GetCycleCapacityCalculationQuery(Guid CycleCapacityId)
    : IRequest<Response<CycleCapacityCalculationDto>>;
