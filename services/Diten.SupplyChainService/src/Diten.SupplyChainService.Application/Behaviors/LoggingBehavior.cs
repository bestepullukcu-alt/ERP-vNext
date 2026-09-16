using MediatR;
using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Behaviors;
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger, RequestContext context) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { { "TenantId", context.Scope.TenantId }, { "LegalEntityId", context.Scope.LegalEntityId }, { "CorrelationId", context.CorrelationId } });
        logger.LogInformation("Handling {Operation}", typeof(TRequest).Name); return await next();
    }
}
