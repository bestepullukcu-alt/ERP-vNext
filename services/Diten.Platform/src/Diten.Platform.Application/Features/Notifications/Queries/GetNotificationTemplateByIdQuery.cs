using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetNotificationTemplateByIdQuery(Guid Id) : IRequest<Response<NotificationTemplateDto>>;
