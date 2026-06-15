using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.BusinessReferenceData;

/// <summary>
/// Innermost MediatR behavior for BusinessReferenceData (PSS-012) requests. It maps the module's
/// coded domain exceptions (thrown by handlers/services) to <c>Response&lt;T&gt;.Fail(code, statusCode)</c>,
/// reproducing the exact HTTP status codes the controller used to assign per endpoint. This keeps the
/// controller thin and the handlers returning <c>Response&lt;T&gt;</c> without per-handler try/catch.
///
/// Only requests implementing <see cref="IBusinessReferenceDataRequest"/> are handled; everything else
/// passes through untouched. Unknown exceptions are rethrown so the global pipeline handles them (500).
/// </summary>
public sealed class BusinessReferenceDataExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string ResponseTypeFullName = "Diten.Platform.Application.Common.Response`1";

    private readonly ILogger<BusinessReferenceDataExceptionBehavior<TRequest, TResponse>> _logger;

    public BusinessReferenceDataExceptionBehavior(ILogger<BusinessReferenceDataExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IBusinessReferenceDataRequest)
        {
            return await next();
        }

        try
        {
            return await next();
        }
        catch (Exception ex) when (TryMap(ex, out var error, out var statusCode))
        {
            var failure = TryCreateFailureResponse(error, statusCode);
            if (failure is not null)
            {
                _logger.LogDebug(
                    "BusinessReferenceData request {RequestType} mapped exception {ExceptionType} to {StatusCode}/{Error}.",
                    typeof(TRequest).Name,
                    ex.GetType().Name,
                    statusCode,
                    error);
                return failure;
            }

            throw;
        }
    }

    /// <summary>
    /// Maps a coded domain exception to (errorCode, httpStatus). Returns false for exceptions that are
    /// not part of the BusinessReferenceData coded contract, so they are rethrown to the outer pipeline.
    /// Mirrors the per-endpoint status codes the controller assigned before it was thinned.
    /// </summary>
    private static bool TryMap(Exception ex, out string error, out int statusCode)
    {
        switch (ex)
        {
            case KeyNotFoundException knf:
                error = string.IsNullOrWhiteSpace(knf.Message) ? "not_found" : knf.Message;
                statusCode = 404;
                return true;

            case UnauthorizedAccessException:
                error = "workflow_transition_forbidden";
                statusCode = 403;
                return true;

            case InvalidOperationException ioe:
                var message = ioe.Message ?? string.Empty;

                // 503 — external governance dependency unavailable.
                if (message == "evidence_dependency_unavailable")
                {
                    error = message;
                    statusCode = 503;
                    return true;
                }

                // 404 — no published version is a not-found style condition in the consumer surface.
                if (message == "no_published_version")
                {
                    error = message;
                    statusCode = 404;
                    return true;
                }

                // 409 — conflict/state errors.
                if (message is "duplicate_set_code"
                    or "active_draft_exists"
                    or "concurrency_conflict"
                    or "validation_blockers"
                    or "workflow_instance_missing"
                    or "workflow_task_missing"
                    or "sod_submitter_cannot_approve"
                    or "already_published_different_idempotency"
                    or "already_committed_different_idempotency"
                    or "reference_data_set_retired")
                {
                    error = message;
                    statusCode = 409;
                    return true;
                }

                // 400 — every other coded business validation error (immutable_field:*, invalid_set_status,
                // draft_required*, value/attribute/mapping validation, evidence validation, override/rejection/
                // request-info requirements, publish request validation, idempotency_key_required, etc.).
                error = string.IsNullOrWhiteSpace(message) ? "invalid_request" : message;
                statusCode = 400;
                return true;

            default:
                error = string.Empty;
                statusCode = 0;
                return false;
        }
    }

    private static TResponse? TryCreateFailureResponse(string error, int statusCode)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType
            || responseType.GetGenericTypeDefinition().FullName != ResponseTypeFullName)
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
