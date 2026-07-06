using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Diten.AuthService.Application.Common.Behaviors;

public sealed class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (ValidationException vex)
        {
            // FIX-PASSWORD-POLICY-ERROR-SURFACE — a handler-thrown FluentValidation failure (e.g. the tenant password
            // policy) carries user-actionable messages. Surface them as a 400 with the joined messages, BEFORE the
            // generic 500 below swaps them for "An unexpected error occurred." The AuthGateway reads errors[] into the
            // response detail, so the specific reason reaches the UI.
            var message = string.Join(" ", vex.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
            if (string.IsNullOrWhiteSpace(message))
            {
                message = vex.Message;
            }

            var validationResponse = TryCreateFailureResponse(message, 400);
            if (validationResponse is not null)
            {
                return validationResponse;
            }

            throw;
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
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition().FullName != "Diten.AuthService.Application.Common.Response`1")
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
