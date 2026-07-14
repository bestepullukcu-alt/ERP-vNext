using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

// MOD-0027-FU04B — delegates straight to the adapter; carries no logic of its own.
public sealed class DispatchNotificationByEventCodeHandler
    : IRequestHandler<DispatchNotificationByEventCodeCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationEventDispatchAdapter _adapter;

    public DispatchNotificationByEventCodeHandler(INotificationEventDispatchAdapter adapter) => _adapter = adapter;

    public Task<Response<NotificationDispatchDto>> Handle(
        DispatchNotificationByEventCodeCommand request, CancellationToken ct) =>
        _adapter.DispatchByEventCodeAsync(request.Request, ct);
}
