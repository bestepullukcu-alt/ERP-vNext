using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D12 / R7 — one legal entity's timer switch. The first write creates its row (no row = off); switching
/// on records who and why; every write carries the version the admin read (F11), so two admins cannot silently undo each
/// other.
///
/// <para><b>T1b — off means off now.</b> Switching an entity off closes the running timers of the people whose primary
/// seat is in it, at the switch time, <c>SwitchedOff</c> (pack §13). A timer the loop here misses (a seat moved in
/// between) is closed by that person's next read, at the same switch time.</para>
///
/// <para>The legal entity id is not looked up: legal entities are MDM's (another service), and a switch row for an
/// unknown entity is inert — nobody's primary seat resolves to it.</para>
/// </summary>
public sealed class SetLegalEntityTimerSwitchHandler
    : IRequestHandler<SetLegalEntityTimerSwitchCommand, Response<LegalEntityTimeSettingDto>>
{
    private readonly ILegalEntityTimeSettingRepository _settings;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;
    private readonly ITimerSegmentRepository _segments;
    private readonly ITimeEntryOrgGateway _org;
    private readonly ITimerService _timer;
    private readonly ILogger<SetLegalEntityTimerSwitchHandler> _logger;

    public SetLegalEntityTimerSwitchHandler(
        ILegalEntityTimeSettingRepository settings,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        TimeProvider clock,
        ITimerSegmentRepository segments,
        ITimeEntryOrgGateway org,
        ITimerService timer,
        ILogger<SetLegalEntityTimerSwitchHandler> logger)
    {
        _logger = logger;
        _segments = segments;
        _org = org;
        _timer = timer;
        _settings = settings;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Response<LegalEntityTimeSettingDto>> Handle(SetLegalEntityTimerSwitchCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow();
        var reason = string.IsNullOrWhiteSpace(request.Request.Reason) ? null : request.Request.Reason.Trim();
        var row = await _settings.GetByLegalEntityIdAsync(request.LegalEntityId, ct);
        bool written;

        if (row is null)
        {
            // No row yet (= off). Creating one is a write like any other: the caller must have read "no row" (F11).
            row = new LegalEntityTimeSetting
            {
                TenantId = _tenantContext.TenantId,
                LegalEntityId = request.LegalEntityId,
                TimerEnabled = request.Request.TimerEnabled,
                ChangedAtUtc = now,
                ChangedByUserId = _currentUser.UserId,
                Reason = reason,
                CreatedBy = _currentUser.UserId.ToString()
            };
            written = request.Request.ExpectedVersion == 0 && await _settings.TryCreateAsync(row, ct);
        }
        else
        {
            row.TimerEnabled = request.Request.TimerEnabled;
            row.ChangedAtUtc = now;
            row.ChangedByUserId = _currentUser.UserId;
            row.Reason = reason;
            row.UpdatedBy = _currentUser.UserId.ToString();
            written = await _settings.UpdateAsync(row, request.Request.ExpectedVersion, ct);
        }

        if (written && !row.TimerEnabled)
        {
            await CloseRunningTimersAsync(request.LegalEntityId, now, ct);
        }

        return written
            ? Response<LegalEntityTimeSettingDto>.Success(WorkCategoryMapping.ToDto(row), correlationId: request.CorrelationId)
            : Response<LegalEntityTimeSettingDto>.Fail(
                "The switch changed meanwhile; reload and retry.", 409,
                TimeEntryReasonCodes.TimerSwitchConcurrencyConflict, request.CorrelationId);
    }

    private async Task CloseRunningTimersAsync(Guid legalEntityId, DateTimeOffset switchedAt, CancellationToken ct)
    {
        // Best effort (v2 F9): the switch is already saved; a close that fails here is logged, never turned into a 500,
        // and that person's next read closes the segment at this same switch time.
        foreach (var segment in await _segments.ListRunningAsync(int.MaxValue, ct))
        {
            try
            {
                if ((await _org.PrimarySeatAsync(segment.UserId, ct))?.LegalEntityId == legalEntityId)
                {
                    await _timer.CloseAsync(segment, switchedAt, TimerStopReason.SwitchedOff, null, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "time-entry.timer.switch_off_close_failed SegmentId={SegmentId}; the next read closes it.", segment.Id);
            }
        }
    }
}
