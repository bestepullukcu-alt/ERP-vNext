namespace Diten.SupplyChainService.Api.Features.Returns;
public static class ReturnContractError
{
    public static object Create(string code, int status, Guid correlation, string? message = null) =>
        new { error = new { code, message = message ?? code.Replace('_', ' '), correlationId = correlation }, contractVersion = "v1" };
    // SHIPMENT-BUNDLE 3.4.0 (MOD-0186 §34): the contract Error's `details` (line, INVENTORY code, stored receipt values).
    public static object Create(string code, int status, Guid correlation, System.Text.Json.Nodes.JsonObject? details) => details is null
        ? Create(code, status, correlation)
        : new { error = new { code, message = code.Replace('_', ' '), details, correlationId = correlation }, contractVersion = "v1" };
}
