using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Application.Features.RecordCodes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Admin;

/// <summary>
/// MOD-0040-FU02 §4.3 — a tenant's record-code group prefix, for the platform administrator only (<c>PlatformAdminOnly</c>:
/// a live platform-administrator record, never a partner administrator — <c>PlatformActor</c> is NOT used). The value is
/// never typed: the bodyless PUT copies the effective document <c>OrgPrefix</c>. Reached over the existing
/// <c>/api/admin/tenants/{everything}</c> Gateway route; no UI in v1.
/// </summary>
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/record-code-group-prefix")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class TenantRecordCodeGroupPrefixController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TenantRecordCodeGroupPrefixController(IMediator mediator) => _mediator = mediator;

    [HttpPut]
    [HasPermission("platform.record-code-group-prefixes.update")]
    public async Task<IActionResult> Align(Guid tenantId, CancellationToken cancellationToken)
    {
        var bodyPresent = Request.ContentLength > 0
            || (Request.ContentLength is null && Request.Headers.TransferEncoding.Count > 0);
        return CreateActionResultInstance(
            await _mediator.Send(new AlignRecordCodeGroupPrefixCommand(tenantId, bodyPresent), cancellationToken));
    }

    [HttpGet]
    [HasPermission("platform.record-code-group-prefixes.read")]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetRecordCodeGroupPrefixQuery(tenantId), cancellationToken));
}
