using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.API.Observability;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/notifications")]
[Authorize(Policy = "PlatformActor")]
public sealed class NotificationsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public NotificationsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("tenant-settings/{tenantId:guid}")]
    [HasPermission("Platform.Notifications.Configure")]
    public async Task<IActionResult> GetTenantSettings(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantMessagingSettingsQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("tenant-settings/{tenantId:guid}")]
    [HasPermission("Platform.Notifications.Configure")]
    public async Task<IActionResult> UpsertTenantSettings(
        Guid tenantId,
        [FromBody] TenantMessagingSettingsUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpsertTenantMessagingSettingsCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("tenant-settings/{tenantId:guid}")]
    [HasPermission("Platform.Notifications.Configure")]
    public async Task<IActionResult> DeleteTenantSettings(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteTenantMessagingSettingsCommand(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tenant-settings/{tenantId:guid}/resolved")]
    [HasPermission("Platform.Notifications.Read")]
    public async Task<IActionResult> GetResolvedTenantSettings(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetResolvedTenantMessagingSettingsQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("templates/{templateKey}")]
    [HasPermission("Platform.Notifications.Templates.Read")]
    public async Task<IActionResult> GetPlatformDefaultTemplate(
        string templateKey,
        [FromQuery] string locale = "en",
        [FromQuery] NotificationChannelCode channel = NotificationChannelCode.Email,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetNotificationTemplateByKeyQuery(templateKey, locale, channel), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tenant-settings/{tenantId:guid}/templates/{templateKey}")]
    [HasPermission("Platform.Notifications.Templates.Read")]
    public async Task<IActionResult> GetTenantTemplate(
        Guid tenantId,
        string templateKey,
        [FromQuery] string locale = "en",
        [FromQuery] NotificationChannelCode channel = NotificationChannelCode.Email,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetNotificationTemplateByKeyQuery(templateKey, locale, channel, tenantId, IsPlatformDefault: false), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("templates")]
    [HasPermission("Platform.Notifications.Templates.Create")]
    public async Task<IActionResult> CreatePlatformDefaultTemplate(
        [FromBody] NotificationTemplateUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateNotificationTemplateCommand(null, request with { IsPlatformDefault = true }), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tenant-settings/{tenantId:guid}/templates")]
    [HasPermission("Platform.Notifications.Templates.Create")]
    public async Task<IActionResult> CreateTenantTemplate(
        Guid tenantId,
        [FromBody] NotificationTemplateUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateNotificationTemplateCommand(tenantId, request with { IsPlatformDefault = false }), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("templates/{id:guid}")]
    [HasPermission("Platform.Notifications.Templates.Update")]
    public async Task<IActionResult> UpdatePlatformDefaultTemplate(
        Guid id,
        [FromBody] NotificationTemplateUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateNotificationTemplateCommand(id, null, request with { IsPlatformDefault = true }), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("tenant-settings/{tenantId:guid}/templates/{id:guid}")]
    [HasPermission("Platform.Notifications.Templates.Update")]
    public async Task<IActionResult> UpdateTenantTemplate(
        Guid tenantId,
        Guid id,
        [FromBody] NotificationTemplateUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateNotificationTemplateCommand(id, tenantId, request with { IsPlatformDefault = false }), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("templates/{id:guid}/archive")]
    [HasPermission("Platform.Notifications.Templates.Archive")]
    public async Task<IActionResult> ArchiveTemplate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ArchiveNotificationTemplateCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("email/queue")]
    [HasPermission("Platform.Notifications.Dispatches.Queue")]
    public async Task<IActionResult> QueueEmail(
        [FromQuery] Guid targetTenantId,
        [FromBody] QueueEmailNotificationRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new QueueEmailNotificationCommand(targetTenantId, request, _correlationContext.CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("dispatches")]
    [HasPermission("Platform.Notifications.Dispatches.Read")]
    public async Task<IActionResult> GetDispatches(
        [FromQuery] Guid targetTenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetNotificationDispatchListQuery(targetTenantId, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("dispatches/{id:guid}")]
    [HasPermission("Platform.Notifications.Dispatches.Read")]
    public async Task<IActionResult> GetDispatchById(Guid id, [FromQuery] Guid targetTenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetNotificationDispatchByIdQuery(targetTenantId, id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("dispatches/{id:guid}/sent")]
    [HasPermission("Platform.Notifications.Dispatches.Queue")]
    public async Task<IActionResult> MarkSent(Guid id, [FromQuery] Guid targetTenantId, [FromQuery] string? providerMessageId, CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkNotificationDispatchSentCommand(targetTenantId, id, providerMessageId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("dispatches/{id:guid}/failed")]
    [HasPermission("Platform.Notifications.Dispatches.Queue")]
    public async Task<IActionResult> MarkFailed(Guid id, [FromQuery] Guid targetTenantId, [FromQuery] string errorCode, [FromQuery] string errorMessage, CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkNotificationDispatchFailedCommand(targetTenantId, id, errorCode, errorMessage), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("dispatches/{id:guid}/cancel")]
    [HasPermission("Platform.Notifications.Dispatches.Queue")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] Guid targetTenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new CancelNotificationDispatchCommand(targetTenantId, id), ct);
        return CreateActionResultInstance(response);
    }
}
