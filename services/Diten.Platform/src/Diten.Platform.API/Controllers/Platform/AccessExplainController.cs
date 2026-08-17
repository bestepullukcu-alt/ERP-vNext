using Diten.Platform.API.Authorization.Explain;
using Diten.Platform.API.Controllers.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// AG-STEP-011 / MOD-0018-FU14 Group B — self-explain endpoint. Plain <c>[Authorize]</c> (any authenticated subject
/// explains its OWN access); deliberately NOT gated by <c>[HasPermission]</c> or an actor policy, and it takes NO
/// subject / tenant parameter (the subject + tenant come only from the authenticated context). Cross-user explain is
/// deferred (OD-FU14-09); the reserved marker <c>platform.access.explain</c> is NOT used on this self route.
/// </summary>
[ApiController]
[Route("api/platform/access/explain")]
public sealed class AccessExplainController : CustomBaseController
{
    private readonly ISelfAccessExplainService _explainService;

    public AccessExplainController(ISelfAccessExplainService explainService)
    {
        _explainService = explainService;
    }

    /// <summary>
    /// Explains the authenticated caller's own effective access for one bounded (permissionKey, moduleCode[, featureCode]).
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> ExplainMyAccess(
        [FromQuery] string? permissionKey,
        [FromQuery] string? moduleCode,
        [FromQuery] string? featureCode,
        CancellationToken ct)
    {
        var response = await _explainService.ExplainAsync(User, permissionKey, moduleCode, featureCode, ct);
        return CreateActionResultInstance(response);
    }
}
