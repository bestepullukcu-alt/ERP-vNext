using System.Text.Json.Nodes;
namespace Diten.SupplyChainService.Api.Features.SupplierPerformance;
/// <summary>The SUPPLIER-PERFORMANCE v1 Error object: { error: { code, message, details?, correlationId }, contractVersion }.</summary>
public static class SupplierPerformanceContractError
{
    public static object Create(string code, int status, Guid correlation, JsonObject? details = null)
    {
        var message = status switch
        {
            401 => "Authentication required.",
            403 => "Permission required.",
            404 => "Not found.",
            503 => "Outcome unavailable; retry with the same key.",
            _ => "Supplier performance request could not be completed."
        };
        return details is null
            ? new { error = new { code, message, correlationId = correlation }, contractVersion = "v1" }
            : new { error = new { code, message, details, correlationId = correlation }, contractVersion = "v1" };
    }
}
