using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetNotificationTemplateListQuery(
    Guid? TenantId,
    bool IsPlatformDefault,
    string? Status = null,
    string? Locale = null,
    string? Channel = null,
    string? TemplateKey = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Response<IReadOnlyList<NotificationTemplateDto>>>;
