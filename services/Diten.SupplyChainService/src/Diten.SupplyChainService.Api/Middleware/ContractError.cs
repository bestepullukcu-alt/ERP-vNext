namespace Diten.SupplyChainService.Api.Middleware;
public static class ContractError
{
    public static object Create(string code, string message, Guid correlationId) => new { error = new { code, message, correlationId }, contractVersion = "v1" };
    public static object Create(string code, string message, Guid correlationId, System.Text.Json.Nodes.JsonObject? details) => details is null
        ? Create(code, message, correlationId)
        : new { error = new { code, message, details, correlationId }, contractVersion = "v1" };
}
