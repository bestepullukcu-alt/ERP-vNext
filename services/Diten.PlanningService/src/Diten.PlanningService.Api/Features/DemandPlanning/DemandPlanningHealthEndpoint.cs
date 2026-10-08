using Diten.PlanningService.Persistence.Features.DemandPlanning;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

public static class DemandPlanningHealthEndpoint
{
    public static RouteHandlerBuilder MapDemandPlanningHealth(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/health", async (IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await services.GetRequiredService<DemandPlanningMongoContext>()
                    .PingAsync(timeout.Token);
                return Results.Ok(new { status = "Healthy", service = "Diten.PlanningService" });
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Json(new { status = "Unhealthy", service = "Diten.PlanningService" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous();
    }
}
