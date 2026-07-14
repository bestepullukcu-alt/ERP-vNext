using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

// MOD-0027-FU04B — thin MediatR wrapper over INotificationEventDispatchAdapter, so a producer can either inject the
// adapter service OR send this command. FU04B only DEFINES it; producers wiring it up is a separate follow-up
// (FU04B-Tenant / FU04D).
public sealed record DispatchNotificationByEventCodeCommand(NotificationEventDispatchRequest Request)
    : IRequest<Response<NotificationDispatchDto>>;
