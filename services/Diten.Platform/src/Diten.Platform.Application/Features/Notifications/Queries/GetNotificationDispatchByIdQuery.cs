using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetNotificationDispatchByIdQuery(Guid TenantId, Guid DispatchId) : IRequest<Response<NotificationDispatchDto>>;
