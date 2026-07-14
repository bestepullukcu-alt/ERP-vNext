using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

// MOD-0027-FU03 — Notification Event Catalog write commands (platform-owned; PlatformActor + events.manage).

public sealed record SyncNotificationEventsFromManifestCommand
    : IRequest<Response<NotificationEventSyncResultDto>>;

public sealed record UpdateNotificationEventCommand(Guid Id, UpdateNotificationEventRequest Request)
    : IRequest<Response<NotificationEventDefinitionDto>>;

public sealed record ArchiveNotificationEventCommand(Guid Id)
    : IRequest<Response<NotificationEventDefinitionDto>>;
