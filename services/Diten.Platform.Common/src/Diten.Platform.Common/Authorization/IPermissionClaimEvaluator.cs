using System.Security.Claims;

namespace Diten.Platform.Common.Authorization;

/// <summary>
/// Evaluates one exact permission already issued in an authenticated principal.
/// It does not validate JWTs or resolve roles, grants, entitlements, or remote decisions.
/// </summary>
public interface IPermissionClaimEvaluator
{
    bool HasPermission(ClaimsPrincipal? principal, string? permission);
}
