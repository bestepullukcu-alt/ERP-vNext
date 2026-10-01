using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Models.Inventory;

/// <summary>MOD-0173 permission keys the FU01 screens gate on (pack §14) — defined by MOD-0173, not new keys.</summary>
public static class InventoryPermissionKeys
{
    public const string Read = "inventory.stock.read";
    public const string CreateMovements = "inventory.movements.create";
}

/// <summary>What the two FU01 controllers share: the Gateway door and the contract-shaped error answer.</summary>
public static class InventoryProxy
{
    public const string ContractVersion = "v1";

    /// <summary>The Gateway client for this request, or null when the principal carries no tenant.</summary>
    public static InventoryGatewayClient? Gateway(HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user)
    {
        var tenantId = user.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(tenantId)) return null;

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(request);
        // W-0173-02: the token's company wins; without one, the user's selected company travels as X-Legal-Entity-Id.
        var tokenHasCompany = CompanySelection.TokenLegalEntity(user) is not null;
        var company = tokenHasCompany ? null : CompanySelection.SelectedCompany(request, user);
        return new InventoryGatewayClient(http, gatewayUrl, token, tenantId, company, companyRequired: !tokenHasCompany);
    }

    /// <summary>
    /// A refusal in the contract's <c>Error</c> shape, with the message already in the reader's language. The
    /// status is the service's own (0 = Gateway unreachable → 503); the code is kept so support can trace it.
    /// </summary>
    public static IActionResult Error(int status, string? code, string message, string? correlationId) =>
        new JsonResult(new
        {
            error = new { code = code ?? "ERROR", message, correlationId },
            contractVersion = ContractVersion
        })
        { StatusCode = status == 0 ? StatusCodes.Status503ServiceUnavailable : status };
}
