using MediatR;
using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Behaviors;
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
 where TRequest : notnull where TResponse : IResponse<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try { return await next(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError("Operation failed: {ExceptionType}", ex.GetType().Name); return TResponse.Fail("INVALID_REQUEST", 500, "Operation could not be completed."); }
    }
}
