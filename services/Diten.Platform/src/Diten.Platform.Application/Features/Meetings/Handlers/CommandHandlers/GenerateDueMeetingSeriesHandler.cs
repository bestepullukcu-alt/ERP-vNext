using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

/// <summary>
/// MOD-0357 S11 — turn each due series into its NEXT single meeting instance, exactly once per occurrence.
/// Mirrors <c>GenerateDueRecurringTasksHandler</c>'s own shape and its central decision: the failure mode is
/// deliberately "miss", not "duplicate" (claim BEFORE create — see that handler's own doc comment for why the
/// reverse order is the wrong one to pick).
///
/// <para><b>KS1 — no third creation path.</b> The FIRST instance (no <see cref="MeetingSeries.LastGeneratedMeetingId"/>
/// yet, or <see cref="MeetingSeries.ChainAsFollowUp"/> false) goes through the ordinary <c>CreateMeetingCommand</c>;
/// every later instance while <c>ChainAsFollowUp</c> is true goes through <c>ScheduleFollowUpMeetingCommand</c>
/// (K6) — never a bespoke insert.</para>
///
/// <para><b>KS3 — the organizer is the SERIES', never the caller's.</b> This handler runs with no user context
/// (the sweep calls it inside <c>TenantScope.Begin</c>, no HTTP request behind it); both create paths are
/// handed <see cref="MeetingSeries.OrganizerUserId"/> EXPLICITLY, so neither ever falls back to
/// <c>ICurrentUserContext</c>, which would answer <c>Guid.Empty</c> here.</para>
/// </summary>
public sealed class GenerateDueMeetingSeriesHandler
    : IRequestHandler<GenerateDueMeetingSeriesCommand, Response<GenerateDueMeetingSeriesResponse>>
{
    private const int DefaultMaxSeries = 200;

    private readonly IMeetingSeriesRepository _series;
    private readonly IMediator _mediator;
    private readonly ILogger<GenerateDueMeetingSeriesHandler> _logger;

    public GenerateDueMeetingSeriesHandler(
        IMeetingSeriesRepository series, IMediator mediator, ILogger<GenerateDueMeetingSeriesHandler> logger)
    {
        _series = series;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Response<GenerateDueMeetingSeriesResponse>> Handle(
        GenerateDueMeetingSeriesCommand command, CancellationToken ct)
    {
        var now = (command.NowUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var max = command.MaxSeries <= 0 ? DefaultMaxSeries : command.MaxSeries;

        // ListActiveAsync already excludes deleted rows and inactive series (KS5); NextDueOccurrence checks
        // both again, the same "the repository filters it is not something a rule about cancelled work should
        // rely on" posture GenerateDueRecurringTasksHandler already takes.
        var rules = (await _series.ListActiveAsync(ct)).Take(max).ToList();

        var generated = 0;
        var alreadyDone = 0;
        var failed = 0;

        foreach (var series in rules)
        {
            ct.ThrowIfCancellationRequested();

            if (MeetingSeriesSchedule.NextDueOccurrence(series, now) is not { } occurrence)
            {
                continue;
            }

            var processInstanceId = MeetingSeriesSchedule.ProcessInstanceId(series.Id, occurrence);

            // Already made — the comparison is only possible because the stamp is DETERMINISTIC (same series +
            // same occurrence start always produce the same string).
            if (string.Equals(series.LastProcessInstanceId, processInstanceId, StringComparison.Ordinal))
            {
                alreadyDone++;
                continue;
            }

            try
            {
                // CLAIM FIRST (KS2). The expected-version write is what makes two overlapping sweeps produce
                // ONE meeting: both compute the same occurrence, both attempt this write, exactly one wins.
                var claimedVersion = series.Version;
                series.LastProcessInstanceId = processInstanceId;
                series.LastGeneratedAt = occurrence;

                if (!await _series.UpdateAsync(series, claimedVersion, ct))
                {
                    // Another pass claimed this occurrence first — not an error, the guard working.
                    alreadyDone++;
                    continue;
                }

                var meetingId = await GenerateInstanceAsync(series, occurrence, command.CorrelationId, ct);
                if (meetingId is null)
                {
                    failed++;
                    _logger.LogWarning(
                        "meetings.series.generate_failed SeriesId={SeriesId} ProcessInstanceId={ProcessInstanceId} "
                        + "CorrelationId={CorrelationId}. The occurrence stays claimed and will NOT be retried — "
                        + "a duplicate meeting is worse than one missed occurrence.",
                        series.Id, processInstanceId, command.CorrelationId);
                    continue;
                }

                // Best-effort display stamp — LastGeneratedMeetingId is read-only UI convenience (see the
                // entity's own doc comment), never something a failure here should roll the meeting back for.
                series.LastGeneratedMeetingId = meetingId;
                if (!await _series.UpdateAsync(series, series.Version, ct))
                {
                    _logger.LogWarning(
                        "meetings.series.stamp_failed SeriesId={SeriesId} MeetingId={MeetingId} "
                        + "CorrelationId={CorrelationId}. The meeting was created; only the display pointer did not update.",
                        series.Id, meetingId, command.CorrelationId);
                }

                generated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                // One series' failure must not abort the tenant's other series — the same rule every other
                // sweep in this codebase already applies one level down.
                _logger.LogWarning(
                    ex,
                    "meetings.series.rule_failed SeriesId={SeriesId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    series.Id, ex.GetType().Name, command.CorrelationId);
            }
        }

        return Response<GenerateDueMeetingSeriesResponse>.Success(
            new GenerateDueMeetingSeriesResponse(rules.Count, generated, alreadyDone, failed),
            200,
            command.CorrelationId);
    }

    private async Task<Guid?> GenerateInstanceAsync(
        MeetingSeries series, DateTimeOffset occurrenceStart, string correlationId, CancellationToken ct)
    {
        var endAt = occurrenceStart.AddMinutes(Math.Max(1, series.DurationMinutes));

        // KS1 — the first instance is ALWAYS a plain create (nothing to chain to yet), and so is every instance
        // of a series that opted out of chaining.
        if (series.LastGeneratedMeetingId is not { } sourceMeetingId || !series.ChainAsFollowUp)
        {
            var createRequest = new CreateMeetingRequest(
                Title: series.Name,
                MeetingTypeId: series.MeetingTypeId,
                StartAt: occurrenceStart,
                EndAt: endAt,
                Location: series.Location,
                OrganizerUserId: series.OrganizerUserId,
                Description: null,
                FollowUpOfMeetingId: null,
                AttendeeUserIds: series.AttendeeUserIds);

            var createResult = await _mediator.Send(new CreateMeetingCommand(createRequest, correlationId), ct);
            return createResult.IsSuccessful && createResult.Data is not null ? createResult.Data.Id : null;
        }

        // K6 — every later instance is chained as a follow-up of the PREVIOUS one, the identical mechanism a
        // manually-scheduled continuation uses: same carry-forward, same cross-link, no second engine.
        var followUpRequest = new ScheduleFollowUpMeetingRequest(
            StartAt: occurrenceStart,
            EndAt: endAt,
            Title: series.Name,
            MeetingTypeId: series.MeetingTypeId,
            Location: series.Location,
            OrganizerUserId: series.OrganizerUserId,
            Description: null,
            AttendeeUserIds: series.AttendeeUserIds,
            // K11 — deterministic per (series, occurrence): a retried claim (this handler's own claim-then-
            // create ordering already prevents that, but the follow-up command's own idempotency is defense in
            // depth, the same "two independent guards" posture the bridge commands already take) resolves to
            // the SAME meeting rather than a second one.
            IdempotencyKey: MeetingSeriesSchedule.ProcessInstanceId(series.Id, occurrenceStart));

        var followUpResult = await _mediator.Send(
            new ScheduleFollowUpMeetingCommand(sourceMeetingId, followUpRequest, correlationId), ct);
        return followUpResult.IsSuccessful && followUpResult.Data is not null ? followUpResult.Data.MeetingId : null;
    }
}
