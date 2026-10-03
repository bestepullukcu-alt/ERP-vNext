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
        catch (Diten.Platform.Application.Features.Audit.TransactionOwnedAuditRefusedException)
        {
            // INTX FIX2 — a refused audit record is NEVER turned into a response here: it must reach the API's exception
            // handler as itself (503 / 500 with its code). It is an InvalidOperationException, and the arm below would
            // make it a 400 carrying the internal sentence the day the Fail lookup further down starts to succeed —
            // until now only that lookup's failure kept it out. Rethrown by type, not by accident.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {RequestType}", typeof(TRequest).Name);
            
            var errorMessage = "An unexpected error occurred.";
            var statusCode = 500;

            if (ex is InvalidOperationException)
            {
                errorMessage = ex.Message;
                statusCode = 400;
            }
            else if (ex is FluentValidation.ValidationException valEx)
            {
                errorMessage = valEx.Errors.FirstOrDefault()?.ErrorMessage ?? valEx.Message;
                statusCode = 400;
            }

            var response = TryCreateFailureResponse(errorMessage, statusCode);
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

        var failMethod = FailMethodFinderOverride.Value?.Invoke(responseType) ?? responseType.GetMethod(
            "Fail",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(int)]);
        return failMethod is null ? default : (TResponse?)failMethod.Invoke(null, [error, statusCode]);
    }

    /// <summary>
    /// Test seam (flow-local, so parallel tests are unaffected): a lookup that FINDS a (string, int) Fail, to measure that
    /// the audit refusal still passes through when the lookup above is one day corrected.
    /// </summary>
    internal static readonly AsyncLocal<Func<Type, MethodInfo?>?> FailMethodFinderOverride = new();
}
