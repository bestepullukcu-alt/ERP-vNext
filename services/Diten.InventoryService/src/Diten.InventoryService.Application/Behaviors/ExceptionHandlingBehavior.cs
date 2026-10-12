using Diten.InventoryService.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Behaviors;

/// <summary>
/// Pipeline behavior 3/4 (pipeline-behaviors.md) — mirrors the ProcurementService template. An unexpected exception
/// becomes a 500 without a contract code; nothing was committed because the posting transaction aborts first.
/// </summary>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(
        ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogError(ex, "Unhandled exception for {RequestName}", requestName);

            var responseType = typeof(TResponse);
            if (responseType.IsGenericType &&
                responseType.GetGenericTypeDefinition() == typeof(Response<>))
            {
                var innerType = responseType.GetGenericArguments()[0];
                var failMethod = typeof(Response<>)
                    .MakeGenericType(innerType)
                    .GetMethod("Fail", [typeof(string), typeof(int)])!;
                return (TResponse)failMethod.Invoke(null, ["An unexpected error occurred.", 500])!;
            }

            throw;
        }
    }
}
