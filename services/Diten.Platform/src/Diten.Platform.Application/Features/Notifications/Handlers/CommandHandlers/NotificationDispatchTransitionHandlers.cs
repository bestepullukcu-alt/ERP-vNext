using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Prometheus;

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
    // BL-406 — ops-only counter. Meeting-related permanent failures ALSO get the organizer notification + attendee
    // badge below; every producer (meeting or not) gets this. Static/self-registered, same shape as
    // Diten.Platform.API.Observability.BackgroundJobExecutionLogMetricsDecorator's own counters — no DI needed.
    private static readonly Counter PermanentlyFailedCounter = Metrics.CreateCounter(
        "notification_dispatch_permanently_failed",
        "Notification dispatches whose final retry attempt was exhausted with no further retry due.",
        new CounterConfiguration { LabelNames = new[] { "template_key", "is_meeting_related" } });

    private const string MeetingTemplateKeyPrefix = "platform.meetings.";
    private const string MeetingUndeliveredEventCode = "platform.meetings.invite-undelivered";

    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MarkNotificationDispatchFailedHandler>? _logger;
    private readonly IMeetingRepository? _meetings;
    private readonly IMeetingAttendeeRepository? _meetingAttendees;
    private readonly IUserNotificationRepository? _userNotifications;

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
    }

    public async Task<Response<NotificationDispatchDto>> Handle(MarkNotificationDispatchFailedCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (request.RetryCount is { } retryCount)
        {
            dispatch.RetryCount = Math.Max(0, retryCount);
        }
        if (request.NextRetryAt is { } nextRetryAt)
        {
            dispatch.NextRetryAt = nextRetryAt;
        }
        if (!dispatch.TryMarkFailed(request.ErrorCode, request.ErrorMessage, DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);

        // BL-406 — idempotency guard: PermanentlyFailedNotifiedAt is set ONLY here and never cleared, so a second
        // command that (for whatever reason — a duplicate job execution, a re-entrant sweep) claims
        // IsPermanentFailure=true over an ALREADY-notified dispatch does not fire the ops counter, the log line,
        // or the organizer notification a second time.
        var isFirstPermanentFailure = request.IsPermanentFailure && dispatch.PermanentlyFailedNotifiedAt is null;
        if (isFirstPermanentFailure)
        {
            dispatch.PermanentlyFailedNotifiedAt = DateTimeOffset.UtcNow;
        }

        await _repository.UpdateAsync(dispatch, ct);
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
            await HandlePermanentFailureAsync(dispatch, ct);
        }

        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }

    /// <summary>
    /// BL-406 — everything that happens ONCE, at the moment a dispatch's failure becomes permanent. Ops
    /// log line + counter fire for EVERY permanently-failed dispatch (rule 3); the organizer notification + the
    /// attendee "mail undelivered" badge fire ONLY for meeting-related mail (rule 1/2). Never allowed to fail the
    /// primary transition above — this runs strictly after that transition and its own event has already
    /// published.
    /// </summary>
    private async Task HandlePermanentFailureAsync(NotificationDispatch dispatch, CancellationToken ct)
    {
        var isMeetingRelated = dispatch.TemplateKey.StartsWith(MeetingTemplateKeyPrefix, StringComparison.OrdinalIgnoreCase);

        PermanentlyFailedCounter.WithLabels(dispatch.TemplateKey, isMeetingRelated ? "true" : "false").Inc();
        _logger?.LogWarning(
            "email.dispatch.permanently_failed DispatchId={DispatchId} TenantId={TenantId} TemplateKey={TemplateKey} "
            + "IsMeetingRelated={IsMeetingRelated} RetryCount={RetryCount} ErrorCode={ErrorCode} CorrelationId={CorrelationId}",
            dispatch.Id, dispatch.TenantId, dispatch.TemplateKey, isMeetingRelated, dispatch.RetryCount, dispatch.ErrorCode, dispatch.CorrelationId);

        if (!isMeetingRelated)
        {
            return;
        }

        if (dispatch.CausationId is not { } meetingId || dispatch.MeetingAttendeeUserId is not { } attendeeUserId)
        {
            // Meeting-templated mail with no (meetingId, attendeeUserId) attribution — cannot happen from
            // MeetingInviteMailer's own 1:1 dispatch path, but a future producer reusing the same template key
            // without setting both fields must not throw here; the ops log/counter above already fired.
            _logger?.LogWarning(
                "email.dispatch.permanently_failed.meeting_attribution_missing DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
            return;
        }

        // K12's own posture, one level down: an organizer notification failing to write must never surface as
        // this command failing (the dispatch's own Failed transition has already been committed and published
        // above).
        try
        {
            if (_meetingAttendees is not null)
            {
                await _meetingAttendees.MarkMailUndeliveredAsync(meetingId, attendeeUserId, DateTimeOffset.UtcNow, ct);
            }

            if (_meetings is null || _userNotifications is null)
            {
                return;
            }

            var meeting = await _meetings.GetByIdAsync(meetingId, ct);
            if (meeting is null)
            {
                return;
            }

            // The dispatch is 1:1 (BL-406's own MeetingInviteMailer change) — its single `To` entry IS the
            // attendee this failure is about. DisplayName falls back to the email so the organizer's notification
            // never reads as blank.
            var recipient = dispatch.To.Count > 0 ? dispatch.To[0] : null;
            var personLabel = recipient?.DisplayName ?? recipient?.Email ?? attendeeUserId.ToString();

            await _userNotifications.CreateAsync(
                new UserNotification
                {
                    TenantId = dispatch.TenantId,
                    UserId = meeting.OrganizerUserId,
                    EventCode = MeetingUndeliveredEventCode,
                    // Data the tenant typed (the meeting's own title) — no sentence composed here; a surface
                    // resolves its label from EventCode + these two pieces of data, same posture as
                    // TaskNotificationService.WriteInAppNotificationsAsync's own doc comment.
                    Title = meeting.Title,
                    Body = personLabel,
                    TargetUrl = $"/Meetings/{meeting.Id}",
                    Severity = UserNotificationSeverity.Warning
                },
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "email.dispatch.permanently_failed.organizer_notification_write_failed DispatchId={DispatchId} MeetingId={MeetingId}",
                dispatch.Id, meetingId);
        }
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
