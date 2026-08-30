using Diten.ManagementGovernanceService.Application.Features.Dws;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Route("internal/local-test/v1/process-modeling/improvements")]
[Authorize(AuthenticationSchemes = LocalTestSecurity.LocalTestAuthenticationHandler.SchemeName)]
public sealed class PerformanceImprovementController : CustomBaseController
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("")]
    public IActionResult RootUnavailable() => Unavailable();

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public IActionResult Unavailable()
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(User.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
            return FromResponse(Response<object>.Fail("process_model_unauthenticated", 401));
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var header) || header.Count != 1 ||
            !Guid.TryParse(header[0], out var supplied) || supplied != tenant)
            return FromResponse(Response<object>.Fail("process_model_bad_request", 400));
        return FromResponse(Response<object>.Fail("performance_improvement_authority_unavailable", 503));
    }
}
