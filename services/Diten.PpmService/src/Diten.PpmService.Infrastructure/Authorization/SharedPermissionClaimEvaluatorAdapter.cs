using Diten.Platform.Common.Authorization;
using Diten.PpmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.PpmService.Infrastructure.Authorization;

/// <summary>
/// Adapts MOD-0117's application boundary to the PSS-approved signed-JWT claim evaluator.
/// </summary>
public sealed class SharedPermissionClaimEvaluatorAdapter(
    IHttpContextAccessor httpContextAccessor,
    IPermissionClaimEvaluator permissionClaimEvaluator) : IEffectivePermissionEvaluator
{
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        return Task.FromResult(
            permissionClaimEvaluator.HasPermission(
                httpContextAccessor.HttpContext?.User,
                permission));
    }
}
