using FluentValidation;
using FluentValidation.Results;
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

            // Attach machine-readable codes for the coded (password.*) failures so the frontend can localize them.
            // `message` (English) stays as the back-compat `detail`/logging fallback; non-coded failures carry no
            // ErrorCodes and the frontend falls back to `detail`.
            var errorCodes = ExtractPasswordErrorCodes(vex.Errors);
            var validationResponse = errorCodes.Count > 0
                ? TryCreateFailureResponse(message, errorCodes, 400) ?? TryCreateFailureResponse(message, 400)
                : TryCreateFailureResponse(message, 400);
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

    // Turn coded FluentValidation failures (password.*) into stable descriptors. Non-coded failures (FluentValidation
    // defaults like "NotEmptyValidator", or other features' rules) are ignored here so only password paths carry codes.
    private static IReadOnlyList<ResponseError> ExtractPasswordErrorCodes(IEnumerable<ValidationFailure> failures)
    {
        var codes = new List<ResponseError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var failure in failures)
        {
            var code = failure.ErrorCode;
            if (string.IsNullOrEmpty(code) || !code.StartsWith(PasswordErrorCodes.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!seen.Add(code))
            {
                continue;
            }

            var @params = failure.CustomState as IReadOnlyDictionary<string, string>;
            codes.Add(new ResponseError(code, @params));
        }

        return codes;
    }

    private static TResponse? TryCreateFailureResponse(string error, int statusCode)
    {
        var responseType = ResolveResponseType();
        if (responseType is null)
        {
            return default;
        }

        var failMethod = responseType.GetMethod(
            "Fail",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(int)]);
        return failMethod is null ? default : (TResponse?)failMethod.Invoke(null, [error, statusCode]);
    }

    private static TResponse? TryCreateFailureResponse(string error, IReadOnlyList<ResponseError> errorCodes, int statusCode)
    {
        var responseType = ResolveResponseType();
        if (responseType is null)
        {
            return default;
        }

        var failMethod = responseType.GetMethod(
            "Fail",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(IReadOnlyList<ResponseError>), typeof(int)]);
        return failMethod is null ? default : (TResponse?)failMethod.Invoke(null, [error, errorCodes, statusCode]);
    }

    private static Type? ResolveResponseType()
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition().FullName != "Diten.AuthService.Application.Common.Response`1")
        {
            return null;
        }

        return responseType;
    }
}
