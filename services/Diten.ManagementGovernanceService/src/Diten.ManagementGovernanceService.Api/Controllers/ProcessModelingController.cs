using System.Security.Claims;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingUserAcceptance;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Route("internal/local-test/v1/process-modeling/models")]
[Authorize(AuthenticationSchemes = LocalTestAuthenticationHandler.SchemeName)]
public sealed class ProcessModelingController(IProcessModelUserAcceptanceQueries queries) : CustomBaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var context = Resolve(ProcessModelingPermissions.ExactPermissions[8]);
        if (context.Error is not null) return context.Error;
        return FromResponse(Response<object>.Success(await queries.ListAsync(context.TenantId, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var context = Resolve(null);
        if (context.Error is not null) return context.Error;
        var value = await queries.GetAsync(context.TenantId, id, cancellationToken);
        if (value is null) return FromResponse(Response<object>.Fail("process_model_not_found", 404));
        if (!HasExactPermission(ProcessModelingPermissions.ExactPermissions[8]))
            return FromResponse(Response<object>.Fail("process_model_permission_denied", 403));
        return FromResponse(Response<object>.Success(value));
    }

    private Trusted Resolve(string? permission)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(User.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
            return Trusted.Rejected(FromResponse(Response<object>.Fail("process_model_unauthenticated", 401)));
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var header) || header.Count != 1 ||
            !Guid.TryParse(header[0], out var supplied) || supplied != tenant)
            return Trusted.Rejected(FromResponse(Response<object>.Fail("process_model_bad_request", 400)));
        if (permission is not null && !HasExactPermission(permission))
            return Trusted.Rejected(FromResponse(Response<object>.Fail("process_model_permission_denied", 403)));
        return new(tenant, actor, null);
    }

    private bool HasExactPermission(string permission) =>
        User.FindAll("permission").Any(x => string.Equals(x.Value, permission, StringComparison.Ordinal));

    private sealed record Trusted(Guid TenantId, Guid ActorId, IActionResult? Error)
    {
        internal static Trusted Rejected(IActionResult error) => new(Guid.Empty, Guid.Empty, error);
    }
}
