using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class MarkNotificationDispatchSentHandler
    : IRequestHandler<MarkNotificationDispatchSentCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    public MarkNotificationDispatchSentHandler(INotificationDispatchRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    public async Task<Response<NotificationDispatchDto>> Handle(MarkNotificationDispatchSentCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (!dispatch.TryMarkSent(request.ProviderMessageId, DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);
        if (!string.IsNullOrWhiteSpace(request.DegradedReason))
        {
            // BL-454 — no new field: a SENT row's error fields say how it was sent when it was not sent in full.
            dispatch.ErrorCode = NotificationDispatch.RetryDegradedErrorCode;
            dispatch.ErrorMessage = $"Sent by a retry from the stored preview ({request.DegradedReason}).";
        }

        await _repository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchSentV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.ProviderMessageId,
                dispatch.RetryCount,
                dispatch.SentAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}

public sealed class MarkNotificationDispatchFailedHandler
    : IRequestHandler<MarkNotificationDispatchFailedCommand, Response<NotificationDispatchDto>>
{
    /// <summary>BL-454 — a conditional close lost to a write that happened after the caller read the row.</summary>
    public const string ReasonDispatchChanged = "DISPATCH_CHANGED";

    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MarkNotificationDispatchFailedHandler>? _logger;
    private readonly IMeetingRepository? _meetings;
    private readonly IMeetingAttendeeRepository? _meetingAttendees;
    private readonly IUserNotificationRepository? _userNotifications;
    private readonly Services.NotificationPermanentFailureEffects _effects;

    public MarkNotificationDispatchFailedHandler(
        INotificationDispatchRepository repository,
        IEventBus eventBus,
        ILogger<MarkNotificationDispatchFailedHandler>? logger = null,
        IMeetingRepository? meetings = null,
        IMeetingAttendeeRepository? meetingAttendees = null,
        IUserNotificationRepository? userNotifications = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _logger = logger;
        _meetings = meetings;
        _meetingAttendees = meetingAttendees;
        _userNotifications = userNotifications;
        _effects = new Services.NotificationPermanentFailureEffects(logger, meetings, meetingAttendees, userNotifications);
    }

    public async Task<Response<NotificationDispatchDto>> Handle(MarkNotificationDispatchFailedCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        // BL-454 — a CONDITIONAL close (the sweep's retry window): only over the exact row the caller read. A send that
        // landed in between has moved Status/Version on, and then this command writes nothing.
        var readVersion = dispatch.Version;
        var readStatus = dispatch.Status;
        if (request.ExpectedVersion is { } expectedVersion
            && (expectedVersion != readVersion || request.ExpectedStatus != readStatus))
        {
            return Response<NotificationDispatchDto>.Fail("The dispatch changed after it was read.", 409, ReasonDispatchChanged);
        }

        if (request.RetryCount is { } retryCount)
        {
            dispatch.RetryCount = Math.Max(0, retryCount);
        }
        if (request.NextRetryAt is { } nextRetryAt)
        {
            dispatch.NextRetryAt = nextRetryAt;
        }
        if (!dispatch.TryMarkFailed(request.ErrorCode, request.ErrorMessage, DateTimeOffset.UtcNow, request.IsPermanentFailure)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);

        // BL-406 — idempotency guard: PermanentlyFailedNotifiedAt is set ONLY here and never cleared, so a second
        // command that (for whatever reason — a duplicate job execution, a re-entrant sweep) claims
        // IsPermanentFailure=true over an ALREADY-notified dispatch does not fire the ops counter, the log line,
        // or the organizer notification a second time.
        var isFirstPermanentFailure = request.IsPermanentFailure && dispatch.PermanentlyFailedNotifiedAt is null;
        if (isFirstPermanentFailure)
        {
            // BL-454 — permanent from this write on, effects still to run: if the publish below throws, the retry
            // sweep finds the row pending and applies them (EmailDispatchSweepJob.RedrivePendingEffectsAsync).
            dispatch.PermanentlyFailedNotifiedAt = NotificationDispatch.PermanentFailurePending;
        }

        if (request.ExpectedVersion is not null)
        {
            if (!await _repository.TryUpdateAsync(dispatch, readVersion, readStatus, ct))
            {
                return Response<NotificationDispatchDto>.Fail("The dispatch changed after it was read.", 409, ReasonDispatchChanged);
            }
        }
        else
        {
            await _repository.UpdateAsync(dispatch, ct);
        }

        await _eventBus.PublishAsync(
            new NotificationDispatchFailedV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.ErrorCode ?? request.ErrorCode,
                dispatch.RetryCount,
                dispatch.NextRetryAt,
                dispatch.FailedAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);

        if (isFirstPermanentFailure)
        {
            await _effects.ApplyAndMarkAsync(dispatch, request.Silent, _repository, ct);
        }

        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }

}

public sealed class CancelNotificationDispatchHandler
    : IRequestHandler<CancelNotificationDispatchCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    public CancelNotificationDispatchHandler(INotificationDispatchRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    public async Task<Response<NotificationDispatchDto>> Handle(CancelNotificationDispatchCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (!dispatch.TryCancel(DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);
        await _repository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchCancelledV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.UpdatedAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}
