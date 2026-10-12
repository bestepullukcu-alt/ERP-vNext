namespace Diten.SupplyChainService.Api.Features.Returns;
public static class ReturnContractError
{
    public static object Create(string code, int status, Guid correlation, string? message = null) =>
        new { error = new { code, message = message ?? code.Replace('_', ' '), correlationId = correlation }, contractVersion = "v1" };
}
