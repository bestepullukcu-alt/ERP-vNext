using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

public interface ITimerReadModel
{
    /// <summary>D3 / §13 — before a timer or week read: closes the person's running segment if its midnight passed, its
    /// task left them, or their switch is off — through the audited system commands, and only when one is due. Never
    /// fails the read (the next read tries again).</summary>
    Task ReconcileAsync(Guid userId, string correlationId, CancellationToken ct = default);

    /// <summary>The person's timer as the screen shows it. Reads only.</summary>
    Task<TimerDto> ReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// The timer's read side: what <c>GET /timer</c> answers and what every timer write answers after it, plus the read-time
/// reconcile the pack puts on every timer and week read (pack §19.1 "Reconcile", D3 "correctness never depends on a
/// scheduler").
/// </summary>
public sealed class TimerReadModel : ITimerReadModel
{
    private readonly ITimerService _timer;
    private readonly ITimerSegmentRepository _segments;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly ILogger<TimerReadModel> _logger;

    public TimerReadModel(
        ITimerService timer,
        ITimerSegmentRepository segments,
        IWorkingHoursProvider workingHours,
        IMediator mediator,
        TimeProvider clock,
        ILogger<TimerReadModel> logger)
    {
        _timer = timer;
        _segments = segments;
        _workingHours = workingHours;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task ReconcileAsync(Guid userId, string correlationId, CancellationToken ct = default)
    {
        try
        {
            switch ((await _timer.PendingCloseAsync(userId, ct)).Kind)
            {
                case TimerPendingClose.LocalMidnight:
                    // No notification from a read: the person is looking at the screen, the banner tells them (D3).
                    await _mediator.Send(new CloseTimersAtLocalMidnightCommand(userId, Notify: false, correlationId), ct);
                    break;
                case TimerPendingClose.Reconcile:
                case TimerPendingClose.SwitchedOff:
                    await _mediator.Send(new ReconcileOrphanTimerCommand(userId, correlationId), ct);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reconciling the timer of {UserId} failed on a read path; the next read retries.", userId);
        }
    }

    public async Task<TimerDto> ReadAsync(Guid userId, CancellationToken ct = default)
    {
        var enabled = await _timer.IsEnabledForAsync(userId, ct);
        var running = await _segments.GetRunningAsync(userId, ct);

        var now = _clock.GetUtcNow();
        var hours = await _workingHours.GetWorkingWindowsAsync(
            userId, DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1), DateOnly.FromDateTime(now.UtcDateTime).AddDays(1), ct);
        var yesterday = hours.LocalDateOf(now).AddDays(-1);
        var closedAtMidnight = await _segments.ListClosedAtMidnightAsync(userId, yesterday, ct);

        return new TimerDto(
            enabled,
            enabled ? null : TimeEntryReasonCodes.TimerDisabledForLegalEntity,
            running is null ? null : ToDto(running),
            closedAtMidnight
                .Select(s => new TimerAutoClosedDto(s.Id, s.LocalDate, s.TaskItemId, s.CategoryCode, s.DurationSeconds))
                .ToList());
    }

    public static TimerSegmentDto ToDto(TimerSegment segment) => new(
        segment.Id,
        segment.TaskItemId,
        segment.CategoryCode,
        segment.StartedAtUtc,
        segment.LocalDate,
        segment.StartSource.ToString(),
        segment.SwitchToken,
        segment.SwitchToken is not null && segment.StartedAtUtc is { } started ? started + TimerRules.UndoWindow : null);
}
