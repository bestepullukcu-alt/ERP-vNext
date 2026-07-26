using Diten.Platform.Application.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Diten.Platform.API.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A blocked workflow transition is an expected BUSINESS outcome, not a crash: logging it at Error made every
        // legitimate approval refusal look like an unhandled exception and polluted the monitoring signal.
        if (exception is WorkflowTransitionBlockedException blocked)
        {
            _logger.LogWarning(
                "Workflow gate blocked a transition: {ReasonCode} — {Message}",
                blocked.Result.BlockingReasonCode,
                blocked.Result.BlockingMessage);
        }
        else
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
        }

        var problemDetails = exception switch
        {
            // 409: the request was understood and the server is working correctly — the workflow refuses it.
            // The reason code travels as an extension named exactly "reason_code", which is the field the frontend
            // bridge reads (Tasks/api.js) to turn the refusal into a message in the user's own language. Without it
            // the client can only show a generic error, and this path used to fall through to a 500.
            WorkflowTransitionBlockedException blockedException => CreateBlockedProblemDetails(blockedException),
            ValidationException validationException => CreateValidationProblemDetails(validationException),
            InvalidOperationException invalidOperationException => CreateProblemDetails(
                invalidOperationException.Message, 
                "Application Error", 
                (int)HttpStatusCode.BadRequest),
            _ => CreateProblemDetails(
                "An unexpected error occurred on the server.", 
                "Server Error", 
                (int)HttpStatusCode.InternalServerError)
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateBlockedProblemDetails(WorkflowTransitionBlockedException ex)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Transition Blocked",
            Status = (int)HttpStatusCode.Conflict,
            // Diagnostic only. The user-facing text comes from the reason code via the frontend's resx bridge, so
            // this English server string is never what is shown on screen.
            Detail = ex.Result.BlockingMessage ?? "The workflow does not permit this transition."
        };

        if (!string.IsNullOrWhiteSpace(ex.Result.BlockingReasonCode))
        {
            problemDetails.Extensions["reason_code"] = ex.Result.BlockingReasonCode;
        }

        return problemDetails;
    }

    private static ProblemDetails CreateValidationProblemDetails(ValidationException ex)
    {
        var firstErrorMessage = ex.Errors.FirstOrDefault()?.ErrorMessage
            ?? "One or more validation failures have occurred.";

        var errors = ex.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                x => x.Key,
                x => x.Select(e => e.ErrorMessage).ToArray()
            );

        return new ValidationProblemDetails(errors)
        {
            Title = "Validation Error",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = firstErrorMessage
        };
    }

    private static ProblemDetails CreateProblemDetails(string detail, string title, int status)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status
        };
    }
}
