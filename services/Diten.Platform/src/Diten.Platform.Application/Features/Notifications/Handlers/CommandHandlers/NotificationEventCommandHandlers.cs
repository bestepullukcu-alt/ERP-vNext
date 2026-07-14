using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class SyncNotificationEventsFromManifestHandler
    : IRequestHandler<SyncNotificationEventsFromManifestCommand, Response<NotificationEventSyncResultDto>>
{
    private readonly INotificationEventManifestSyncService _syncService;
    public SyncNotificationEventsFromManifestHandler(INotificationEventManifestSyncService syncService) => _syncService = syncService;

    public async Task<Response<NotificationEventSyncResultDto>> Handle(SyncNotificationEventsFromManifestCommand request, CancellationToken ct)
    {
        var result = await _syncService.SyncAsync(ct);
        return Response<NotificationEventSyncResultDto>.Success(result);
    }
}

public sealed class UpdateNotificationEventHandler
    : IRequestHandler<UpdateNotificationEventCommand, Response<NotificationEventDefinitionDto>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public UpdateNotificationEventHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<NotificationEventDefinitionDto>> Handle(UpdateNotificationEventCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return Response<NotificationEventDefinitionDto>.Fail("Notification event not found.", 404);

        var r = request.Request;
        // SOFT fields only — HARD (EventCode/OwnerModuleId/Channel/DefaultTemplateKey/RequiredVariables) are manifest-reconciled and immutable here.
        if (r.DisplayNameKey is not null) entity.DisplayNameKey = string.IsNullOrWhiteSpace(r.DisplayNameKey) ? null : r.DisplayNameKey.Trim();
        if (!string.IsNullOrWhiteSpace(r.FallbackDisplayName)) entity.FallbackDisplayName = r.FallbackDisplayName.Trim();
        if (r.Description is not null) entity.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        entity.CanTenantOverride = r.CanTenantOverride;

        if (!string.IsNullOrWhiteSpace(r.DefaultSeverity))
        {
            if (!Enum.TryParse<NotificationEventSeverity>(r.DefaultSeverity, ignoreCase: true, out var sv) || !Enum.IsDefined(sv))
                return Response<NotificationEventDefinitionDto>.Fail("Unknown severity.", 400);
            entity.DefaultSeverity = sv;
        }

        if (!string.IsNullOrWhiteSpace(r.LinkPolicy))
        {
            if (!Enum.TryParse<NotificationEventLinkPolicy>(r.LinkPolicy, ignoreCase: true, out var lp) || !Enum.IsDefined(lp))
                return Response<NotificationEventDefinitionDto>.Fail("Unknown link policy.", 400);
            entity.LinkPolicy = lp;
        }

        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<NotificationEventStatus>(r.Status, ignoreCase: true, out var st) || !Enum.IsDefined(st))
                return Response<NotificationEventDefinitionDto>.Fail("Unknown status.", 400);
            entity.Status = st;
        }

        await _repository.UpdateAsync(entity, ct);
        return Response<NotificationEventDefinitionDto>.Success(entity.ToDto());
    }
}

public sealed class ArchiveNotificationEventHandler
    : IRequestHandler<ArchiveNotificationEventCommand, Response<NotificationEventDefinitionDto>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public ArchiveNotificationEventHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<NotificationEventDefinitionDto>> Handle(ArchiveNotificationEventCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return Response<NotificationEventDefinitionDto>.Fail("Notification event not found.", 404);

        if (entity.Status == NotificationEventStatus.Archived)
            return Response<NotificationEventDefinitionDto>.Fail("Notification event is already archived.", 409);

        entity.Status = NotificationEventStatus.Archived;
        await _repository.UpdateAsync(entity, ct);
        return Response<NotificationEventDefinitionDto>.Success(entity.ToDto());
    }
}
