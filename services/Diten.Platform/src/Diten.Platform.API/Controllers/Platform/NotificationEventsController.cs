using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

// MOD-0027-FU03 — Notification Event Catalog admin/read API. PlatformActor only. Events are manifest-driven
// (no manual create); write surface is sync-from-manifest + SOFT-field update + archive.
[ApiController]
[Route("api/platform/notifications")]
[Authorize(Policy = "PlatformActor")]
public sealed class NotificationEventsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public NotificationEventsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("events")]
    [HasPermission("platform.notifications.events.read")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? ownerModuleId = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? canTenantOverride = null,
        [FromQuery] string? usageType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(
            new GetNotificationEventListQuery(ownerModuleId, channel, status, canTenantOverride, usageType, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("events/{eventCode}")]
    [HasPermission("platform.notifications.events.read")]
    public async Task<IActionResult> GetEventByCode(string eventCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetNotificationEventByCodeQuery(eventCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("events/{eventCode}/template-contract")]
    [HasPermission("platform.notifications.events.read")]
    public async Task<IActionResult> GetTemplateContract(string eventCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetNotificationEventTemplateContractQuery(eventCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("events/sync-from-manifest")]
    [HasPermission("platform.notifications.events.manage")]
    public async Task<IActionResult> SyncFromManifest(CancellationToken ct)
    {
        var response = await _mediator.Send(new SyncNotificationEventsFromManifestCommand(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("events/{id:guid}")]
    [HasPermission("platform.notifications.events.manage")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateNotificationEventRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateNotificationEventCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("events/{id:guid}/archive")]
    [HasPermission("platform.notifications.events.manage")]
    public async Task<IActionResult> ArchiveEvent(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ArchiveNotificationEventCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    // Active template-slot list for the (future) template create/edit UI. Access model (§14): readable by
    // events.read holders AND template authors. Since actor_type=platform_admin auto-passes all permissions and the
    // template UI actor carries templates.read/create/update, this is gated on templates.read for template-author
    // compatibility (event managers are platform admins and auto-pass).
    [HttpGet("template-slots")]
    [HasPermission("platform.notifications.templates.read")]
    public async Task<IActionResult> GetTemplateSlots(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetActiveTemplateSlotsQuery(), ct);
        return CreateActionResultInstance(response);
    }
}
