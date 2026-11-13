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
    string? TemplateKey = null,
    // WP-VW-W5 PLT-NOTIFY — only the dispatches this module asked for (exact, trimmed); null = every source.
    string? SourceModule = null) : IRequest<Response<IReadOnlyList<NotificationDispatchListItemDto>>>;
