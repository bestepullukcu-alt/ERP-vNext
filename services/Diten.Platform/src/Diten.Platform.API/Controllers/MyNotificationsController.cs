using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// BL-025 — a TENANT user's own in-app notifications.
///
/// <para><b>Why this is not a method on <c>Platform/NotificationsController</c>.</b> That controller carries
/// <c>[Authorize(Policy = "PlatformActor")]</c> at CLASS level: it is the platform operator's window onto every
/// tenant's messaging settings, templates and dispatches. A tenant user cannot satisfy that policy, and
/// loosening it per-method would mean a class whose contract says "platform actors only" while three of its
/// methods do not — the kind of exception that the next reader has to discover by testing. So the tenant
/// surface is its own controller, in <c>Controllers/</c> alongside the other <c>api/v1/*</c> tenant endpoints
/// (Tasks, WorkItems, DocumentManagement*).</para>
///
/// <para><b>Why <c>[Authorize]</c> and no <c>[HasPermission]</c>.</b> Every other endpoint here guards a
/// RESOURCE, and a permission answers "may this user act on that resource?". "My own notifications" is not a
/// resource — it is the caller's identity, and the answer is already yes by virtue of being authenticated. A
/// permission key here could only do harm: an admin who forgets to grant it locks users out of their own
/// inbox, and a bell that silently 403s is indistinguishable from a bell with nothing in it — the exact
/// failure mode ("notifications are not arriving" filed against a working system) this feature exists to end.
/// There is no scope to widen either: the caller cannot name a subject, so the permission would gate nothing
/// a permission could meaningfully gate.</para>
///
/// <para><b>Scope is resolved server-side, in the handlers.</b> No action on this controller accepts a user
/// id in any form — route, query, body or header. See <c>GetMyNotificationsQuery</c>.</para>
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class MyNotificationsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public MyNotificationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>The caller's own notifications, unread first, newest first within each group.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetMyNotificationsQuery(page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// Mark one of the caller's notifications read. Somebody else's id answers the same as a missing one —
    /// the endpoint cannot be used to discover that a notification exists.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkMyNotificationReadCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>Mark every unread notification of the caller read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkAllMyNotificationsReadCommand(), ct);
        return CreateActionResultInstance(response);
    }
}
