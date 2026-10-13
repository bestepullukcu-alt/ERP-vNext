using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.ManufacturingService.Application.Behaviors;

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
        catch (BomPersistenceUnavailableException ex) when (IsResponse(out var failMethod))
        {
            // Nothing was written (single transaction): the caller may retry. K2 fail-closed, not a 500.
            _logger.LogWarning(ex, "Persistence unavailable for {RequestName}", typeof(TRequest).Name);
            return (TResponse)failMethod!.Invoke(null, [BomErrorCodes.PersistenceUnavailable, 503])!;
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
                return (TResponse)failMethod.Invoke(null, [BomErrorCodes.InternalError, 500])!;
            }

            throw;
        }
    }

    private static bool IsResponse(out System.Reflection.MethodInfo? failMethod)
    {
        failMethod = null;
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Response<>))
        {
            return false;
        }

        failMethod = typeof(Response<>).MakeGenericType(responseType.GetGenericArguments()[0]).GetMethod("Fail", [typeof(string), typeof(int)]);
        return failMethod is not null;
    }
}
