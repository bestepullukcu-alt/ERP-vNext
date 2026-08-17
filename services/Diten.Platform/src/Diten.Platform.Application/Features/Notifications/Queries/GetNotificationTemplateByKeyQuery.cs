using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetNotificationTemplateByKeyQuery(
    string TemplateKey,
    string Locale,
    NotificationChannelCode Channel,
    Guid? TenantId = null,
    bool IsPlatformDefault = true) : IRequest<Response<NotificationTemplateDto>>;
