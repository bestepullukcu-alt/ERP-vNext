using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

// TenantId: the route's tenant (null = a platform default). See NotificationTemplateScope.
public sealed record GetNotificationTemplateByIdQuery(Guid Id, Guid? TenantId = null) : IRequest<Response<NotificationTemplateDto>>;
