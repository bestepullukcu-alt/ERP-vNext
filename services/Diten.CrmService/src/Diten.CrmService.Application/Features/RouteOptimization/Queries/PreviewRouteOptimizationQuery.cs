using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.RouteOptimization.Queries;

/// <summary>
/// MOD-0155 FU03 — the dry-run route preview (pack §11). A QUERY in every sense: it calls the pure
/// <see cref="IRouteOptimizer"/> over the supplied set and returns the schedule, <b>persisting NOTHING</b> — no
/// PlannedVisit write, no Mongo write, no side effect. It exists so the heuristic can be exercised with real data BEFORE
/// FU05 exists, and later to back an FU05 "preview route" button (the <c>PreviewCycleCapacityCalculation</c> precedent).
/// <para>The request is the <see cref="RouteOptimizationInput"/> DTO verbatim and the answer is the
/// <see cref="RouteOptimizationOutput"/> DTO verbatim, so a caller validates the heuristic over the wire and in-process
/// identically. Over-supply / unfittable input is a 200 with a populated <c>unscheduled[]</c> (the warning is data, not
/// an HTTP error); only a malformed DTO / out-of-range buffer is a 400.</para>
/// </summary>
public sealed record PreviewRouteOptimizationQuery(RouteOptimizationInput Input)
    : IRequest<Response<RouteOptimizationOutput>>;
