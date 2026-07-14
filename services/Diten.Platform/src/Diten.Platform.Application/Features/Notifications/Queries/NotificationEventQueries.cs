using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

// MOD-0027-FU03 — Notification Event Catalog read queries.

public sealed record GetNotificationEventListQuery(
    string? OwnerModuleId = null,
    string? Channel = null,
    string? Status = null,
    bool? CanTenantOverride = null,
    string? UsageType = null,
    int Page = 1,
    int PageSize = 100) : IRequest<Response<IReadOnlyList<NotificationEventDefinitionDto>>>;

public sealed record GetNotificationEventByCodeQuery(string EventCode)
    : IRequest<Response<NotificationEventDefinitionDto>>;

public sealed record GetNotificationEventTemplateContractQuery(string EventCode)
    : IRequest<Response<NotificationEventTemplateContractDto>>;

public sealed record GetActiveTemplateSlotsQuery
    : IRequest<Response<IReadOnlyList<NotificationEventTemplateSlotDto>>>;
