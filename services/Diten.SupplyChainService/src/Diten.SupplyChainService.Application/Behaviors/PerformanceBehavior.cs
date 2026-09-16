using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
namespace Diten.SupplyChainService.Application.Behaviors;
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); try { return await next(); } finally { if (watch.ElapsedMilliseconds > 500) logger.LogWarning("Slow {Operation}: {ElapsedMs}ms", typeof(TRequest).Name, watch.ElapsedMilliseconds); }
    }
}
