using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.TimeEntry;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

public interface ITimerAutoCloseNotifier
{
    /// <summary>Hands over the ONE "your timer ran until midnight" notification for a segment the midnight job closed.</summary>
    Task NotifyAsync(TimerSegment segment, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D3 — the morning notification, event <c>time-entry.timer.auto-closed</c> (declared in the manifest; its
/// templates are T3, so until then the dispatch answers "event not active" and nothing is sent — logged, never thrown).
/// Called only by the midnight job, only for a segment it closed itself, and only after claiming the segment's
/// notification mark — so one person-day never gets two.
/// </summary>
public sealed class TimerAutoCloseNotifier : ITimerAutoCloseNotifier
{
    private readonly INotificationEventDispatchAdapter _dispatch;
    private readonly ITaskNotificationRecipientResolver _recipients;
    private readonly ILogger<TimerAutoCloseNotifier> _logger;

    public TimerAutoCloseNotifier(
        INotificationEventDispatchAdapter dispatch,
        ITaskNotificationRecipientResolver recipients,
        ILogger<TimerAutoCloseNotifier> logger)
    {
        _dispatch = dispatch;
        _recipients = recipients;
        _logger = logger;
    }

    public async Task NotifyAsync(TimerSegment segment, CancellationToken ct = default)
    {
        try
        {
            var recipient = (await _recipients.ResolveAsync([segment.UserId], ct)).FirstOrDefault();
            if (recipient is null)
            {
                _logger.LogInformation("time-entry.timer.auto-closed: no address for {UserId}; not sent.", segment.UserId);
                return;
            }

            var result = await _dispatch.DispatchByEventCodeAsync(new NotificationEventDispatchRequest(
                segment.TenantId,
                TimeEntryNotificationEvents.TimerAutoClosed,
                [new EmailRecipientDto(recipient.Email, recipient.DisplayName)],
                new Dictionary<string, object?>
                {
                    ["localDate"] = segment.LocalDate.ToString("yyyy-MM-dd"),
                    ["durationMinutes"] = segment.DurationSeconds / 60
                },
                CausationId: segment.Id), ct);
            if (!result.IsSuccessful)
            {
                _logger.LogInformation("time-entry.timer.auto-closed for {UserId} not sent: {Reason}.", segment.UserId, result.ReasonCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "time-entry.timer.auto-closed for {UserId} failed; the banner still tells them.", segment.UserId);
        }
    }
}
