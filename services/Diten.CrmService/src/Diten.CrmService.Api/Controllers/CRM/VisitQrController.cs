using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitQr.VisitQrPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.3) — the institution QR codes: state (no payload), history, issue / regenerate, revoke, the
/// print payload, bulk issue / print and the bulk page list. Reads need <c>crm.visit-qr.read</c>; every write and every
/// payload <c>crm.visit-qr.manage</c>. A payload answer is never cached (<c>Cache-Control: no-store</c>) and never logged.
/// TenantId is server-resolved; an institution of another tenant is 404.
/// </summary>
[Authorize]
public sealed class VisitQrController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitQrController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-qr/accounts")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? accountType, [FromQuery] string? city, [FromQuery] string? territoryId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListVisitQrAccountsQuery(status, accountType, city, territoryId, search, page, pageSize), cancellationToken));

    [HttpGet("api/crm/visit-qr/accounts/{accountId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitQrAccountQuery(accountId), cancellationToken));

    [HttpGet("api/crm/visit-qr/accounts/{accountId:guid}/history")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> History(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitQrHistoryQuery(accountId), cancellationToken));

    /// <summary>Issue (no active code) or REGENERATE (an active code; <c>expectedVersion</c> required).</summary>
    [HttpPost("api/crm/visit-qr/accounts/{accountId:guid}/issue")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Issue(Guid accountId, [FromBody] VisitQrIssueInput? input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new IssueVisitQrCommand(accountId, input ?? new VisitQrIssueInput(null, null)), cancellationToken));

    [HttpPost("api/crm/visit-qr/accounts/{accountId:guid}/revoke")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Revoke(Guid accountId, [FromBody] VisitQrRevokeInput? input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new RevokeVisitQrCommand(accountId, input ?? new VisitQrRevokeInput(null, null)), cancellationToken));

    /// <summary>The print payload of the active code (a <c>printed</c> event is written).</summary>
    [HttpGet("api/crm/visit-qr/accounts/{accountId:guid}/payload")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Payload(Guid accountId, CancellationToken cancellationToken)
    {
        NoStore();
        return CreateActionResultInstance(await _mediator.Send(new GetVisitQrPayloadCommand(accountId), cancellationToken));
    }

    [HttpPost("api/crm/visit-qr/bulk-issue")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> BulkIssue([FromBody] VisitQrBulkInput? input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new BulkIssueVisitQrCommand(input ?? new VisitQrBulkInput(null)), cancellationToken));

    [HttpPost("api/crm/visit-qr/bulk-payload")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> BulkPayload([FromBody] VisitQrBulkInput? input, CancellationToken cancellationToken)
    {
        NoStore();
        return CreateActionResultInstance(await _mediator.Send(new BulkVisitQrPayloadCommand(input ?? new VisitQrBulkInput(null)), cancellationToken));
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
