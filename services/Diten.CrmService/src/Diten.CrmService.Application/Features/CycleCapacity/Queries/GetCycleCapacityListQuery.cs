using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>
/// The capacity grid. It returns the authored INPUTS plus the pinned period's identifying fields — never the estimate.
/// <para>Computing the figure here would turn one grid draw into one working-calendar HTTP call per month per row; the
/// estimate has its own endpoint for exactly that reason.</para>
/// </summary>
public sealed record GetCycleCapacityListQuery(
    Guid? CyclePeriodId,
    string? CalendarCountryCode,
    bool IncludeArchived,
    string? Search) : IRequest<Response<CycleCapacityListDto>>;
