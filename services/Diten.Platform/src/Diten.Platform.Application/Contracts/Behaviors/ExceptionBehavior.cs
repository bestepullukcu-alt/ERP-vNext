using MediatR;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Diten.Platform.Application.Contracts.Behaviors;

public sealed class ExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ExceptionBehavior<TRequest, TResponse>> _logger;

    public ExceptionBehavior(ILogger<ExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {RequestType}", typeof(TRequest).Name);
            var response = TryCreateFailureResponse("An unexpected error occurred.", 500);
            if (response is not null)
            {
                return response;
            }

            throw;
        }
    }

    private static TResponse? TryCreateFailureResponse(string error, int statusCode)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition().FullName != "Diten.Platform.Application.Common.Response`1")
        {
            return default;
        }

        var failMethod = responseType.GetMethod(
            "Fail",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(int)]);
        return failMethod is null ? default : (TResponse?)failMethod.Invoke(null, [error, statusCode]);
    }
}
