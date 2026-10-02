using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Diten.AuthService.Api;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            HttpStatusException httpStatusException => (httpStatusException.StatusCode, "Request failed"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled error. Path={Path} TraceId={TraceId}", httpContext.Request.Path, httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(exception, "Handled application error. Path={Path} TraceId={TraceId}", httpContext.Request.Path, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = statusCode;

        // WP-USERS-ERROR-CODES-01 — a pipeline validator's refusal carries its stable codes (the one prefix list and
        // the one helper ExceptionHandlingBehavior uses). ADDITIVE ONLY: without an allowed code the body below is
        // byte for byte what it always was.
        var errorCodes = exception is ValidationException validation ? EnvelopeErrorCodePrefixes.Extract(validation.Errors) : [];
        if (errorCodes.Count > 0)
        {
            await httpContext.Response.WriteAsJsonAsync(new
            {
                title,
                status = statusCode,
                detail = exception.Message,
                traceId = httpContext.TraceIdentifier,
                errorCodes
            }, cancellationToken);

            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
            detail = exception.Message,
            traceId = httpContext.TraceIdentifier
        }, cancellationToken);

        return true;
    }
}
