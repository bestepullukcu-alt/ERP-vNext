using Diten.PlanningService.Persistence.Features.DemandPlanning;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

public sealed class HttpOutboxCorrelationReader(IHttpContextAccessor accessor) : IOutboxCorrelationReader
{
    public Guid? CurrentId
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null || !context.Request.Headers.TryGetValue("X-Correlation-Id", out var values))
                return null;
            if (!PlanningCorrelation.IsValid(context))
                throw new InvalidOperationException("Invalid correlation identifier.");
            return Guid.ParseExact(values[0]!, "D");
        }
    }
}
