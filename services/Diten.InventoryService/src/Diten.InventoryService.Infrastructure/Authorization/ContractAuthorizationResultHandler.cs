using Diten.InventoryService.Infrastructure.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// Gives 401 / 403 refusals the contract <c>Error</c> body and the correlation id instead of an empty response.
/// No <c>error.code</c> is set: the frozen contract declares none for these, and codes outside
/// ASSUMPTION-0173-04 are not invented.
/// </summary>
public sealed class ContractAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged || (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated != true))
        {
            await context.ChallengeAsync();
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, null,
                "A valid bearer token is required.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                "The caller lacks the permission required for this operation.");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
