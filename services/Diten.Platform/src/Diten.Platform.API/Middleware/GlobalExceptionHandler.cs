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
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = exception switch
        {
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
