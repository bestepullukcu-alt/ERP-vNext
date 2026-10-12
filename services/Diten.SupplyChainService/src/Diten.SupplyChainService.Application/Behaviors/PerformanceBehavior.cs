using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Behaviors;
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); try { return await next(); }
        finally
        {
            // O-2: every operation is recorded so a consumer can report p95; the 500 ms line below is A9's observation threshold only.
            ShipmentTelemetry.OperationDuration.Record(watch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>(ShipmentTelemetry.OperationTag, typeof(TRequest).Name));
            if (watch.ElapsedMilliseconds > 500) logger.LogWarning("Slow {Operation}: {ElapsedMs}ms", typeof(TRequest).Name, watch.ElapsedMilliseconds);
        }
    }
}
