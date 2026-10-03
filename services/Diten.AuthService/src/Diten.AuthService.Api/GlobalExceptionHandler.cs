using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Diten.AuthService.Api;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// BL-516 — what a 5xx says. The exception's own message on an unexpected failure is a driver's or a library's text
    /// (a database timeout names host and port); it goes to the server log, never to the caller. The trace id in the
    /// same body is how the two are matched.
    /// </summary>
    public const string ServerErrorDetail = "An unexpected error occurred. Quote the trace id when reporting it.";

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
            // A 4xx is the application's own answer and keeps its sentence; a 5xx keeps nothing of the exception.
            detail = statusCode >= 500 ? ServerErrorDetail : exception.Message,
            traceId = httpContext.TraceIdentifier
        }, cancellationToken);

        return true;
    }
}
