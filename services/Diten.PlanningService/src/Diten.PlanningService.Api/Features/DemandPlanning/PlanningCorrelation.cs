using Microsoft.AspNetCore.Http;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Diten.PlanningService.Cycles.Tests")]

namespace Diten.PlanningService.Api.Features.DemandPlanning;

internal static class PlanningCorrelation
{
    private const string Header = "X-Correlation-Id";

    public static bool IsValid(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(Header, out var values)) return true;
        return values.Count == 1 && Guid.TryParseExact(values[0], "D", out var id) &&
            id != Guid.Empty && string.Equals(values[0], id.ToString("D"), StringComparison.Ordinal);
    }

    public static void Forward(HttpContext? context, HttpRequestMessage request)
    {
        if (context is null || !context.Request.Headers.TryGetValue(Header, out var values)) return;
        if (!IsValid(context)) throw new InvalidOperationException("Invalid correlation identifier.");
        request.Headers.TryAddWithoutValidation(Header, values[0]!);
    }
}
