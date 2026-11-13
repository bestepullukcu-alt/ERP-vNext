using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W5 (W5-BE, K-N9) — the visit notifications' API:
/// <list type="bullet">
/// <item><c>GET api/crm/visit-notifications</c> (paged; <c>from</c>, <c>to</c>, <c>eventCode</c>, <c>status</c>,
/// <c>resourceId</c>, <c>page</c>, <c>pageSize</c>) and <c>GET …/{id}</c> (the preview: event, status, recipients spec +
/// resolved summary, variables — never an e-mail address) — <c>crm.visit-notifications.read</c> OR
/// <c>.settings.manage</c>; a rep sees their own visits, a manager the team, the settings key the tenant;</item>
/// <item><c>GET / PUT api/crm/visit-notifications/settings</c> — the tenant's PV / MSL mailboxes and switches; read with
/// either key, write with <c>.settings.manage</c> (versioned, 409).</item>
/// </list>
/// TenantId is server-resolved.
/// </summary>
[Authorize]
public sealed class VisitNotificationsController : CustomBaseController
{
    private const string Base = "api/crm/visit-notifications";

    private readonly IMediator _mediator;

    public VisitNotificationsController(IMediator mediator) => _mediator = mediator;

    public static bool MayRead(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaims.HasPermission(user, VisitNotificationPermissions.Read)
           || PermissionClaims.HasPermission(user, VisitNotificationPermissions.SettingsManage);

    [HttpGet(Base)]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? eventCode,
        [FromQuery] string? status, [FromQuery] string? resourceId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => MayRead(User)
            ? CreateActionResultInstance(await _mediator.Send(new ListVisitNotificationsQuery(from, to, eventCode, status, resourceId, page, pageSize), cancellationToken))
            : Forbidden();

    [HttpGet(Base + "/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => MayRead(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitNotificationQuery(id), cancellationToken))
            : Forbidden();

    [HttpGet(Base + "/settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
        => MayRead(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitNotificationSettingsQuery(), cancellationToken))
            : Forbidden();

    [HttpPut(Base + "/settings")]
    [HasPermission(VisitNotificationPermissions.SettingsManage)]
    public async Task<IActionResult> PutSettings([FromBody] VisitNotificationSettingsInput input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateVisitNotificationSettingsCommand(input), cancellationToken));

    private IActionResult Forbidden()
        => CreateActionResultInstance(Response<object>.Fail(new[]
        {
            "Reading the visit notifications needs crm.visit-notifications.read or .settings.manage.", "permission_denied"
        }, 403));
}
