using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.RouteOptimization.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.RouteOptimization.Handlers;

/// <summary>
/// The dry-run preview handler. It injects the <see cref="IRouteOptimizer"/> seam and NOTHING that could persist — no
/// repository, no unit of work — so the endpoint could not write even by mistake (AC-ENDPOINT-2). Malformed input is a
/// controlled 400; an over-supply / unfittable set is a 200 whose <c>unscheduled[]</c> carries the warning.
/// </summary>
public sealed class PreviewRouteOptimizationHandler
    : IRequestHandler<PreviewRouteOptimizationQuery, Response<RouteOptimizationOutput>>
{
    /// <summary>Upper bound on the between-visit buffer (pack §4.2: 0 ≤ x ≤ 240).</summary>
    public const int MaxBetweenVisitMinutes = 240;

    private readonly IRouteOptimizer _optimizer;

    public PreviewRouteOptimizationHandler(IRouteOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    public Task<Response<RouteOptimizationOutput>> Handle(
        PreviewRouteOptimizationQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return Task.FromResult(Response<RouteOptimizationOutput>.Fail(errors, 400));
        }

        // Dry-run: pure in-process call, no persistence. Empty / over-supply / unfittable all return 200 — the
        // unscheduled list is data, not an error (D-UNSCHEDULED).
        var output = _optimizer.Optimize(input);
        return Task.FromResult(Response<RouteOptimizationOutput>.Success(output));
    }

    /// <summary>Structural validation of the envelope only. Per-visit shape problems (bad coords, non-positive duration,
    /// too-long visit) are NOT errors here — the engine surfaces them as <c>unscheduled[]</c> reasons (pack §13).</summary>
    private static IReadOnlyList<string> Validate(RouteOptimizationInput? input)
    {
        var errors = new List<string>();
        if (input is null)
        {
            errors.Add("A route optimization input is required.");
            return errors;
        }

        if (input.Period is null)
        {
            errors.Add("A period is required.");
        }
        else if (input.Period.DateTo < input.Period.DateFrom)
        {
            errors.Add("period.dateTo must be on or after period.dateFrom.");
        }

        if (input.BetweenVisitMinutes is < 0 or > MaxBetweenVisitMinutes)
        {
            errors.Add($"betweenVisitMinutes must be between 0 and {MaxBetweenVisitMinutes}.");
        }

        if (input.TravelModel is { } spec
            && !string.IsNullOrWhiteSpace(spec.Kind)
            && !string.Equals(spec.Kind, TravelModelKinds.Haversine, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"travelModel.kind '{spec.Kind}' is not supported; only '{TravelModelKinds.Haversine}' is available.");
        }

        return errors;
    }
}
