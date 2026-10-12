namespace Diten.SupplyChainService.Api.Middleware;
public static class ContractError
{
    public static object Create(string code, string message, Guid correlationId) => new { error = new { code, message, correlationId }, contractVersion = "v1" };
}
