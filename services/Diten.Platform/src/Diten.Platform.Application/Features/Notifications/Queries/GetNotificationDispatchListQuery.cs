using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetNotificationDispatchListQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 50,
    string? Status = null,
    DateTimeOffset? QueuedFrom = null,
    DateTimeOffset? QueuedTo = null,
    string? TemplateKey = null) : IRequest<Response<IReadOnlyList<NotificationDispatchListItemDto>>>;
