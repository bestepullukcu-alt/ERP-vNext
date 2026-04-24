using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.DevEnablementService.Application.Behaviors;

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
