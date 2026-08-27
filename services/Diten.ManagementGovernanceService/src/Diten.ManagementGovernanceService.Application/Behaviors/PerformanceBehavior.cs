using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.ManagementGovernanceService.Application.Behaviors;

public sealed class PerformanceBehavior<TRequest,TResponse>(ILogger<PerformanceBehavior<TRequest,TResponse>> logger) : IPipelineBehavior<TRequest,TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var response = await next();
        if (timer.ElapsedMilliseconds > 500) logger.LogWarning("Slow request {RequestName}: {Elapsed}ms", typeof(TRequest).Name, timer.ElapsedMilliseconds);
        return response;
    }
}
