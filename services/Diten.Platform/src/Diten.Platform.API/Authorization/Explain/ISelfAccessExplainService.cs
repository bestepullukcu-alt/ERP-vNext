using System.Security.Claims;
using Diten.Platform.Application.Common;

namespace Diten.Platform.API.Authorization.Explain;

/// <summary>
/// AG-STEP-011 / MOD-0018-FU14 Group B — backend-only, self-explain observer. Explains ONLY the authenticated caller's
/// own effective-access decision for a single bounded (permission key, module[, feature]) request. The subject + tenant
/// come solely from the authenticated context — there is no caller-controlled subject or tenant. The observer never
/// invokes the real <c>HasPermissionAttribute</c> filter (no admin-lifecycle side effect), never makes or overrides an
/// authorization decision, and never returns a grant inventory.
///
/// Lives in the API layer because it reuses the API-layer <c>PermissionClaimEvaluator</c> (Group A); it injects the
/// Application services (<c>IDataScopeResolver</c>, <c>IAuditService</c>) in the normal API → Application direction.
/// </summary>
public interface ISelfAccessExplainService
{
    Task<Response<SelfAccessExplainResponse>> ExplainAsync(
        ClaimsPrincipal principal,
        string? permissionKey,
        string? moduleCode,
        string? featureCode,
        CancellationToken cancellationToken);
}
