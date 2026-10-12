using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace Diten.InventoryService.Infrastructure.Middleware;

/// <summary>
/// First in the pipeline (pack §8 correlation). <c>X-Correlation-Id</c> is not declared by the frozen contract, so it
/// is optional: absent → the server mints a UUID; present → it must be exactly one UUID, else 400
/// <c>INVALID_REQUEST</c> (echoing a freshly minted id). The id is echoed in the response header of EVERY response,
/// including refusals, and is what the posting persists on its transaction, audit record and outbox event.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        var supplied = context.Request.Headers[InventoryContract.CorrelationHeader];
        var valid = supplied.Count == 0
                    || (supplied.Count == 1 && Guid.TryParse(supplied[0], out var parsed) && parsed != Guid.Empty);

        var correlationId = supplied.Count == 1 && Guid.TryParse(supplied[0], out var fromCaller) && fromCaller != Guid.Empty
            ? fromCaller
            : Guid.NewGuid();
        correlation.Set(correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[InventoryContract.CorrelationHeader] = correlationId.ToString();
            return Task.CompletedTask;
        });

        if (!valid)
        {
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest,
                $"{InventoryContract.CorrelationHeader} must be a single UUID when present.");
            return;
        }

        await _next(context);
    }
}
