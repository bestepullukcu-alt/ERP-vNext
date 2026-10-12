using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Infrastructure.Http;

/// <summary>
/// Writes the contract <c>Error</c> body from middleware (where no controller exists yet): 400 / 401 / 403 refusals
/// raised before a handler runs. Carries the request correlation id like every other response.
/// </summary>
public static class ContractErrorWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string? code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        var correlation = context.RequestServices.GetRequiredService<ICorrelationContext>();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            ContractError.Create(code, message, correlation.CorrelationId, details),
            Json,
            contentType: "application/json");
    }
}
